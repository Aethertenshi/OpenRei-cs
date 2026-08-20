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
        float duration = (float)mpeg.Duration.TotalSeconds;

        var floatBuffer = new float[8192];
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

        var floatBuffer = new float[8192];
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

        short audioFormat = 1;
        short channels = 2;
        int sampleRate = 44100;
        short bitsPerSample = 16;
        byte[]? pcmData = null;

        while (fs.Position < fs.Length)
        {
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
                pcmData = reader.ReadBytes(chunkSize);
                break;
            }
            else
            {
                if (chunkSize > 0) fs.Seek(chunkSize, SeekOrigin.Current);
            }
        }

        if (pcmData == null) throw new InvalidDataException("No audio data chunk found in WAV.");

        float duration = (float)pcmData.Length / (sampleRate * channels * (bitsPerSample / 8));
        return new DecodedAudioData(pcmData, sampleRate, channels, bitsPerSample, duration);
    }
}
