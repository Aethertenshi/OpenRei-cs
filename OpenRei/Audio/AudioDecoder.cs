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
/// Cross-platform audio decoder abstraction for WAV PCM and audio streams.
/// </summary>
public static class AudioDecoder
{
    /// <summary>
    /// Decodes a WAV audio file into uncompressed PCM byte buffers for OpenAL Soft.
    /// </summary>
    public static DecodedAudioData DecodeFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[AudioDecoder Error] Audio file not found: {filePath}");
            return new DecodedAudioData(Array.Empty<byte>(), 44100, 2, 16, 0f);
        }

        try
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

            return new DecodedAudioData(pcmData, sampleRate, channels, bitsPerSample, duration);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AudioDecoder Error] Failed to decode {filePath}: {ex.Message}");
            return new DecodedAudioData(Array.Empty<byte>(), 44100, 2, 16, 0f);
        }
    }
}
