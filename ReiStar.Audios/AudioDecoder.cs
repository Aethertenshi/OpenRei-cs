namespace reistar.Audios;

using System;
using System.IO;
using NLayer;
using NVorbis;

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
/// Converts all input formats into clean 16-bit signed PCM for OpenAL Soft.
/// </summary>
public static class AudioDecoder
{
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
        float duration = (float)mpeg.Duration.TotalSeconds;

        var floatBuffer = new float[16384];
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int samplesRead;
        while ((samplesRead = mpeg.ReadSamples(floatBuffer, 0, floatBuffer.Length)) > 0)
        {
            for (int i = 0; i < samplesRead; i++)
            {
                short sample = (short)Math.Clamp(floatBuffer[i] * 32767f, -32768f, 32767f);
                bw.Write(sample);
            }
        }

        return new DecodedAudioData(ms.ToArray(), sampleRate, channels, 16, duration);
    }

    private static DecodedAudioData DecodeOgg(string filePath)
    {
        using var vorbis = new VorbisReader(filePath);
        int sampleRate = vorbis.SampleRate;
        int channels = vorbis.Channels;
        float duration = (float)vorbis.TotalTime.TotalSeconds;

        var floatBuffer = new float[16384];
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int samplesRead;
        while ((samplesRead = vorbis.ReadSamples(floatBuffer, 0, floatBuffer.Length)) > 0)
        {
            for (int i = 0; i < samplesRead; i++)
            {
                short sample = (short)Math.Clamp(floatBuffer[i] * 32767f, -32768f, 32767f);
                bw.Write(sample);
            }
        }

        return new DecodedAudioData(ms.ToArray(), sampleRate, channels, 16, duration);
    }

    private static DecodedAudioData DecodeWav(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        using var reader = new BinaryReader(fs);

        string riff = new string(reader.ReadChars(4));
        if (riff != "RIFF") throw new InvalidDataException("Not a valid RIFF file.");

        reader.ReadInt32(); // chunk size
        string wave = new string(reader.ReadChars(4));
        if (wave != "WAVE") throw new InvalidDataException("Not a valid WAVE file.");

        short audioFormat = 1; // 1 = PCM, 3 = IEEE Float
        short channels = 2;
        int sampleRate = 44100;
        short bitsPerSample = 16;
        byte[]? rawPcmData = null;

        while (fs.Position < fs.Length)
        {
            if (fs.Position + 8 > fs.Length) break;
            string chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();

            if (chunkId == "fmt ")
            {
                audioFormat = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // byte rate
                reader.ReadInt16(); // block align
                bitsPerSample = reader.ReadInt16();

                int extra = chunkSize - 16;
                if (extra > 0) reader.ReadBytes(extra);
            }
            else if (chunkId == "data")
            {
                rawPcmData = reader.ReadBytes(chunkSize);
                break;
            }
            else
            {
                if (chunkSize > 0) fs.Seek(chunkSize, SeekOrigin.Current);
            }

            // Word align
            if (chunkSize % 2 != 0 && fs.Position < fs.Length)
            {
                fs.Seek(1, SeekOrigin.Current);
            }
        }

        if (rawPcmData == null) throw new InvalidDataException("No audio data chunk found in WAV.");

        // Convert to standard 16-bit PCM
        byte[] pcm16;
        if (audioFormat == 3 || bitsPerSample == 32)
        {
            // 32-bit IEEE Float -> 16-bit PCM
            int sampleCount = rawPcmData.Length / 4;
            pcm16 = new byte[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++)
            {
                float fSample = BitConverter.ToSingle(rawPcmData, i * 4);
                short s16 = (short)Math.Clamp(fSample * 32767f, -32768f, 32767f);
                pcm16[i * 2] = (byte)(s16 & 0xFF);
                pcm16[i * 2 + 1] = (byte)((s16 >> 8) & 0xFF);
            }
        }
        else if (bitsPerSample == 24)
        {
            // 24-bit PCM -> 16-bit PCM
            int sampleCount = rawPcmData.Length / 3;
            pcm16 = new byte[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++)
            {
                int s24 = (rawPcmData[i * 3 + 0] << 8) | (rawPcmData[i * 3 + 1] << 16) | ((sbyte)rawPcmData[i * 3 + 2] << 24);
                short s16 = (short)(s24 >> 16);
                pcm16[i * 2] = (byte)(s16 & 0xFF);
                pcm16[i * 2 + 1] = (byte)((s16 >> 8) & 0xFF);
            }
        }
        else if (bitsPerSample == 8)
        {
            // 8-bit unsigned PCM -> 16-bit signed PCM
            int sampleCount = rawPcmData.Length;
            pcm16 = new byte[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++)
            {
                short s16 = (short)((rawPcmData[i] - 128) * 256);
                pcm16[i * 2] = (byte)(s16 & 0xFF);
                pcm16[i * 2 + 1] = (byte)((s16 >> 8) & 0xFF);
            }
        }
        else
        {
            // Already 16-bit PCM
            pcm16 = rawPcmData;
        }

        float duration = (float)pcm16.Length / (sampleRate * channels * 2);
        return new DecodedAudioData(pcm16, sampleRate, channels, 16, duration);
    }
}
