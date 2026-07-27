using Silk.NET.OpenAL;

namespace OpenRei.Audio;

/// <summary>
/// Pre-buffered low-latency sound effect player for instant hitsounds.
/// </summary>
public class SoundEffect
{
    private uint _bufferId;
    private readonly List<uint> _sourcePool = new();
    private int _poolIndex;

    public SoundEffect() { }

    public SoundEffect(string filePath, int poolSize = 8)
    {
        var data = AudioDecoder.DecodeFile(filePath);
        LoadPcmData(data, poolSize);
    }

    public SoundEffect(DecodedAudioData data, int poolSize = 8)
    {
        LoadPcmData(data, poolSize);
    }

    public SoundEffect(byte[] pcmData, int sampleRate, int channels = 2, int bitsPerSample = 16, int poolSize = 8)
    {
        LoadPcmData(pcmData, sampleRate, channels, bitsPerSample, poolSize);
    }

    public void LoadPcmData(DecodedAudioData data, int poolSize = 8)
    {
        if (data != null)
        {
            LoadPcmData(data.PcmData, data.SampleRate, data.Channels, data.BitsPerSample, poolSize);
        }
    }

    public void LoadPcmData(byte[] pcmData, int sampleRate, int channels = 2, int bitsPerSample = 16, int poolSize = 8)
    {
        if (!AudioEngine.IsInitialized || pcmData.Length == 0) return;

        if (_bufferId == 0)
        {
            _bufferId = AudioEngine.AL.GenBuffer();
        }

        BufferFormat format = (channels, bitsPerSample) switch
        {
            (1, 8) => BufferFormat.Mono8,
            (1, 16) => BufferFormat.Mono16,
            (2, 8) => BufferFormat.Stereo8,
            (2, 16) => BufferFormat.Stereo16,
            _ => BufferFormat.Stereo16
        };

        unsafe
        {
            fixed (byte* ptr = pcmData)
            {
                AudioEngine.AL.BufferData(_bufferId, format, ptr, pcmData.Length, sampleRate);
            }
        }

        // Initialize voice pool for simultaneous hitsounds
        if (_sourcePool.Count == 0)
        {
            for (int i = 0; i < poolSize; i++)
            {
                uint source = AudioEngine.AL.GenSource();
                AudioEngine.AL.SetSourceProperty(source, SourceInteger.Buffer, (int)_bufferId);
                _sourcePool.Add(source);
            }
        }
    }

    /// <summary>
    /// Triggers immediate hitsound playback.
    /// </summary>
    public void Play(float volume = 1.0f, float pitch = 1.0f)
    {
        if (!AudioEngine.IsInitialized || _sourcePool.Count == 0) return;

        uint source = _sourcePool[_poolIndex];
        _poolIndex = (_poolIndex + 1) % _sourcePool.Count;

        AudioEngine.AL.SourceStop(source);
        AudioEngine.AL.SetSourceProperty(source, SourceFloat.Gain, volume);
        AudioEngine.AL.SetSourceProperty(source, SourceFloat.Pitch, pitch);
        AudioEngine.AL.SourcePlay(source);
    }
}
