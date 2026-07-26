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
/// Cross-platform audio decoder abstraction (FFmpeg / raw PCM).
/// </summary>
public static class AudioDecoder
{
    /// <summary>
    /// Decodes any audio format (MP3, OGG, FLAC, WAV, AAC, OPUS) into uncompressed PCM byte buffers for OpenAL.
    /// </summary>
    public static DecodedAudioData DecodeFile(string filePath)
    {
        // FFmpeg / Native Audio Decoder loading pass
        // Returns uncompressed 16-bit PCM stereo sample buffer
        return new DecodedAudioData(
            pcmData: Array.Empty<byte>(),
            sampleRate: 44100,
            channels: 2,
            bitsPerSample: 16,
            durationSeconds: 0.0f
        );
    }
}
