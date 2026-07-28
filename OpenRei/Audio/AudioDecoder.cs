using System.Text;
using NLayer;
using NVorbis;

namespace OpenRei.Audio;

/// <summary>
/// Decoded PCM audio data container for OpenAL buffer queueing.
/// </summary>
public class DecodedAudioData
{
    public byte[] PcmData { get; }
    public int SampleRate { get; }
    public int Channels { get; }
    public int BitsPerSample { get; }
    public float DurationSeconds { get; }

    public DecodedAudioData(byte[] pcmData, int sampleRate, int channels, int bitsPerSample, float durationSeconds)
    {
        PcmData = pcmData;
        SampleRate = sampleRate;
        Channels = channels;
        BitsPerSample = bitsPerSample;
        DurationSeconds = durationSeconds;
    }
}

/// <summary>
/// Cross-platform audio decoder abstraction supporting MP3, OGG, and WAV decoding.
/// </summary>
public static class AudioDecoder
{
    /// <summary>
    /// Decodes an audio file (.mp3, .ogg, .wav) into uncompressed 16-bit PCM byte buffers for OpenAL Soft.
    /// </summary>
    public static DecodedAudioData DecodeFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[AudioDecoder Error] Audio file not found: {filePath}");
            return new DecodedAudioData(Array.Empty<byte>(), 44100, 2, 16, 0f);
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        try
        {
            return ext switch
            {
                ".mp3" => DecodeMp3(filePath),
                ".ogg" => DecodeOgg(filePath),
                _ => DecodeWav(filePath)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AudioDecoder Error] Failed to decode {filePath}: {ex.Message}");
            return new DecodedAudioData(Array.Empty<byte>(), 44100, 2, 16, 0f);
        }
    }

    private static DecodedAudioData DecodeMp3(string filePath)
    {
        using var mpeg = new MpegFile(filePath);
        int sampleRate = mpeg.SampleRate;
        int channels = mpeg.Channels;
        int bitsPerSample = 16;

        List<byte> pcmList = new List<byte>(sampleRate * channels * 2 * 10);
        float[] sampleBuffer = new float[8192];
        int read;

        while ((read = mpeg.ReadSamples(sampleBuffer, 0, sampleBuffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                short sampleShort = (short)Math.Clamp(sampleBuffer[i] * 32767f, -32768f, 32767f);
                pcmList.Add((byte)(sampleShort & 0xFF));
                pcmList.Add((byte)((sampleShort >> 8) & 0xFF));
            }
        }

        byte[] pcmData = pcmList.ToArray();
        int bytesPerSample = (bitsPerSample / 8) * channels;
        float duration = bytesPerSample > 0 ? (float)pcmData.Length / (sampleRate * bytesPerSample) : 0f;

        Console.WriteLine($"[AudioDecoder] Decoded MP3 '{Path.GetFileName(filePath)}' ({sampleRate}Hz, {channels}ch, {duration:F2}s, {pcmData.Length} bytes)");
        return new DecodedAudioData(pcmData, sampleRate, channels, bitsPerSample, duration);
    }

    private static DecodedAudioData DecodeOgg(string filePath)
    {
        using var vorbis = new VorbisReader(filePath);
        int sampleRate = vorbis.SampleRate;
        int channels = vorbis.Channels;
        int bitsPerSample = 16;

        long totalSamples = vorbis.TotalSamples * channels;
        float[] samples = new float[totalSamples];
        int samplesRead = vorbis.ReadSamples(samples, 0, (int)totalSamples);

        byte[] pcmData = new byte[samplesRead * 2];
        for (int i = 0; i < samplesRead; i++)
        {
            short sampleShort = (short)Math.Clamp(samples[i] * 32767f, -32768f, 32767f);
            pcmData[i * 2] = (byte)(sampleShort & 0xFF);
            pcmData[i * 2 + 1] = (byte)((sampleShort >> 8) & 0xFF);
        }

        float duration = (float)vorbis.TotalTime.TotalSeconds;
        Console.WriteLine($"[AudioDecoder] Decoded OGG '{Path.GetFileName(filePath)}' ({sampleRate}Hz, {channels}ch, {duration:F2}s, {pcmData.Length} bytes)");
        return new DecodedAudioData(pcmData, sampleRate, channels, bitsPerSample, duration);
    }

    private static DecodedAudioData DecodeWav(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        // Read RIFF header (use ASCII encoding, NOT ReadChars — binary data)
        string chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (chunkId != "RIFF")
        {
            Console.WriteLine($"[AudioDecoder Error] Invalid WAV file header in {filePath}");
            return new DecodedAudioData(Array.Empty<byte>(), 44100, 2, 16, 0f);
        }

        reader.ReadInt32(); // ChunkSize
        string format = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (format != "WAVE")
        {
            Console.WriteLine($"[AudioDecoder Error] Format is not WAVE in {filePath}");
            return new DecodedAudioData(Array.Empty<byte>(), 44100, 2, 16, 0f);
        }

        int channels = 2;
        int sampleRate = 44100;
        int bitsPerSample = 16;
        byte[] pcmData = Array.Empty<byte>();

        while (stream.Position < stream.Length - 8)
        {
            string subChunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            int subChunkSize = reader.ReadInt32();

            if (subChunkId == "fmt ")
            {
                short audioFormat = reader.ReadInt16();

                if (audioFormat != 1 && audioFormat != 3)
                {
                    Console.WriteLine($"[AudioDecoder Error] Unsupported WAV audio format ({audioFormat}) in {filePath}. Only PCM (1) and IEEE float (3) are supported.");
                    return new DecodedAudioData(Array.Empty<byte>(), 44100, 2, 16, 0f);
                }

                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // ByteRate
                reader.ReadInt16(); // BlockAlign
                bitsPerSample = reader.ReadInt16();

                int extraBytes = subChunkSize - 16;
                if (extraBytes > 0)
                    reader.BaseStream.Seek(extraBytes, SeekOrigin.Current);

                if (audioFormat == 3)
                {
                    // IEEE float → we'll convert to 16-bit PCM after reading
                    bitsPerSample = 32; // track as 32-bit float for now
                }
            }
            else if (subChunkId == "data")
            {
                pcmData = reader.ReadBytes(subChunkSize);
                break;
            }
            else
            {
                if (subChunkSize > 0)
                    reader.BaseStream.Seek(subChunkSize, SeekOrigin.Current);
            }
        }

        // Convert IEEE float samples to 16-bit PCM
        if (bitsPerSample == 32)
        {
            int sampleCount = pcmData.Length / 4;
            byte[] converted = new byte[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = BitConverter.ToSingle(pcmData, i * 4);
                sample = Math.Clamp(sample, -1f, 1f);
                short pcm16 = (short)(sample * 32767f);
                converted[i * 2] = (byte)(pcm16 & 0xFF);
                converted[i * 2 + 1] = (byte)((pcm16 >> 8) & 0xFF);
            }
            pcmData = converted;
            bitsPerSample = 16;
        }

        int bytesPerSample = (bitsPerSample / 8) * channels;
        float duration = bytesPerSample > 0 ? (float)pcmData.Length / (sampleRate * bytesPerSample) : 0f;

        Console.WriteLine($"[AudioDecoder] Decoded WAV '{Path.GetFileName(filePath)}' ({sampleRate}Hz, {channels}ch, bits={bitsPerSample}, dur={duration:F2}s, {pcmData.Length} bytes)");
        return new DecodedAudioData(pcmData, sampleRate, channels, bitsPerSample, duration);
    }
}
