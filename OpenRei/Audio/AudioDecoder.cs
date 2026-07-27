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

        // Allocate float sample buffer
        long totalSampleCount = mpeg.Length;
        float[] samples = new float[totalSampleCount];
        int samplesRead = mpeg.ReadSamples(samples, 0, (int)totalSampleCount);

        byte[] pcmData = new byte[samplesRead * 2];
        for (int i = 0; i < samplesRead; i++)
        {
            short sampleShort = (short)Math.Clamp(samples[i] * 32767f, -32768f, 32767f);
            pcmData[i * 2] = (byte)(sampleShort & 0xFF);
            pcmData[i * 2 + 1] = (byte)((sampleShort >> 8) & 0xFF);
        }

        float duration = mpeg.Duration.TotalSeconds > 0 ? (float)mpeg.Duration.TotalSeconds : (float)samplesRead / (sampleRate * channels);
        Console.WriteLine($"[AudioDecoder] Decoded MP3 '{Path.GetFileName(filePath)}' ({sampleRate}Hz, {channels}ch, {duration:F2}s)");
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
        Console.WriteLine($"[AudioDecoder] Decoded OGG '{Path.GetFileName(filePath)}' ({sampleRate}Hz, {channels}ch, {duration:F2}s)");
        return new DecodedAudioData(pcmData, sampleRate, channels, bitsPerSample, duration);
    }

    private static DecodedAudioData DecodeWav(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        // Read RIFF header
        string chunkId = new string(reader.ReadChars(4));
        if (chunkId != "RIFF")
        {
            Console.WriteLine($"[AudioDecoder Error] Invalid WAV file header in {filePath}");
            return new DecodedAudioData(Array.Empty<byte>(), 44100, 2, 16, 0f);
        }

        reader.ReadInt32(); // ChunkSize
        string format = new string(reader.ReadChars(4));
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
            string subChunkId = new string(reader.ReadChars(4));
            int subChunkSize = reader.ReadInt32();

            if (subChunkId == "fmt ")
            {
                short audioFormat = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // ByteRate
                reader.ReadInt16(); // BlockAlign
                bitsPerSample = reader.ReadInt16();

                int extraBytes = subChunkSize - 16;
                if (extraBytes > 0)
                    reader.BaseStream.Seek(extraBytes, SeekOrigin.Current);
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

        int bytesPerSample = (bitsPerSample / 8) * channels;
        float duration = bytesPerSample > 0 ? (float)pcmData.Length / (sampleRate * bytesPerSample) : 0f;

        Console.WriteLine($"[AudioDecoder] Decoded WAV '{Path.GetFileName(filePath)}' ({sampleRate}Hz, {channels}ch, {duration:F2}s)");
        return new DecodedAudioData(pcmData, sampleRate, channels, bitsPerSample, duration);
    }
}
