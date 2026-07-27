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
    private DecodedAudioData? _pendingData;
    private int _pendingPoolSize = 8;

    /// <summary>
    /// Indicates whether any voice in this sound effect pool is currently playing.
    /// </summary>
    public bool IsPlaying
    {
        get
        {
            EnsureHandlesAndUpload();
            if (!AudioEngine.IsInitialized || _sourcePool.Count == 0) return false;
            foreach (var source in _sourcePool)
            {
                AudioEngine.AL.GetSourceProperty(source, GetSourceInteger.SourceState, out int state);
                if (state == (int)SourceState.Playing) return true;
            }
            return false;
        }
    }

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
            _pendingData = data;
            _pendingPoolSize = poolSize;
            EnsureHandlesAndUpload();
        }
    }

    public void LoadPcmData(byte[] pcmData, int sampleRate, int channels = 2, int bitsPerSample = 16, int poolSize = 8)
    {
        _pendingData = new DecodedAudioData(pcmData, sampleRate, channels, bitsPerSample, 0f);
        _pendingPoolSize = poolSize;
        EnsureHandlesAndUpload();
    }

    private void EnsureHandlesAndUpload()
    {
        if (!AudioEngine.IsInitialized) return;

        if (_bufferId == 0)
        {
            _bufferId = AudioEngine.AL.GenBuffer();
        }

        if (_pendingData != null && _pendingData.PcmData.Length > 0)
        {
            var pcmData = _pendingData.PcmData;
            int sampleRate = _pendingData.SampleRate;

            BufferFormat format = (_pendingData.Channels, _pendingData.BitsPerSample) switch
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
                for (int i = 0; i < _pendingPoolSize; i++)
                {
                    uint source = AudioEngine.AL.GenSource();
                    AudioEngine.AL.SetSourceProperty(source, SourceInteger.Buffer, (int)_bufferId);
                    _sourcePool.Add(source);
                }
            }

            _pendingData = null; // Upload complete
        }
    }

    /// <summary>
    /// Triggers immediate hitsound playback.
    /// </summary>
    public void Play(float volume = 1.0f, float pitch = 1.0f)
    {
        EnsureHandlesAndUpload();
        if (!AudioEngine.IsInitialized || _sourcePool.Count == 0) return;

        uint source = _sourcePool[_poolIndex];
        _poolIndex = (_poolIndex + 1) % _sourcePool.Count;

        AudioEngine.AL.SourceStop(source);
        AudioEngine.AL.SetSourceProperty(source, SourceFloat.Gain, volume);
        AudioEngine.AL.SetSourceProperty(source, SourceFloat.Pitch, pitch);
        AudioEngine.AL.SourcePlay(source);
    }
}
