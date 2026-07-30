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
    private Task<DecodedAudioData?>? _loadTask;
    private bool _playRequested;
    private float _queuedVolume;
    private float _queuedPitch;
    private float _volume = 1f;

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (!AudioEngine.IsInitialized) return;
            foreach (var source in _sourcePool)
                AudioEngine.AL.SetSourceProperty(source, SourceFloat.Gain, _volume);
        }
    }

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

    /// <summary>
    /// Creates a SoundEffect that decodes asynchronously on a background thread.
    /// Play() auto-fires once data is ready.
    /// </summary>
    public static SoundEffect CreateAsync(string filePath, int poolSize = 8)
    {
        var sfx = new SoundEffect();
        sfx._pendingPoolSize = poolSize;
        sfx._loadTask = AudioCache.GetOrDecodeAsync(filePath);
        AudioCache.Track(sfx);
        return sfx;
    }

    internal void CheckPendingLoad()
    {
        if (_loadTask == null || _pendingData != null) return;

        if (_loadTask.IsCompleted)
        {
            _pendingData = _loadTask.Result;
            _loadTask = null;

            if (_playRequested)
            {
                EnsureHandlesAndUpload();
                if (_sourcePool.Count > 0)
                {
                    uint source = _sourcePool[_poolIndex];
                    _poolIndex = (_poolIndex + 1) % _sourcePool.Count;
                    AudioEngine.AL.SourceStop(source);
                    AudioEngine.AL.SetSourceProperty(source, SourceFloat.Gain, _queuedVolume);
                    AudioEngine.AL.SetSourceProperty(source, SourceFloat.Pitch, _queuedPitch);
                    AudioEngine.AL.SourcePlay(source);
                }
                _playRequested = false;
            }
        }
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

        if (_loadTask != null)
        {
            CheckPendingLoad();
            if (_pendingData == null) return;
        }

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
                    AudioEngine.AL.SetSourceProperty(source, (SourceInteger)0x1033, 1);
                    AudioEngine.AL.SetSourceProperty(source, SourceInteger.Buffer, (int)_bufferId);
                    _sourcePool.Add(source);
                }
            }

            _pendingData = null; // Upload complete
        }
    }

    /// <summary>
    /// Triggers immediate hitsound playback. If async load is still in progress,
    /// queues the play to fire automatically when the data arrives.
    /// </summary>
    public void Play(float volumeMultiplier = 1.0f, float pitch = 1.0f)
    {
        if (!AudioEngine.IsInitialized) return;

        float gain = Math.Clamp(_volume * volumeMultiplier, 0f, 1f);

        // Async load still in progress → queue
        if (_loadTask != null && !_loadTask.IsCompleted)
        {
            _playRequested = true;
            _queuedVolume = gain;
            _queuedPitch = pitch;
            return;
        }

        // Async load just completed
        if (_loadTask != null)
        {
            CheckPendingLoad();
        }

        EnsureHandlesAndUpload();
        if (!AudioEngine.IsInitialized || _sourcePool.Count == 0) return;

        uint source = _sourcePool[_poolIndex];
        _poolIndex = (_poolIndex + 1) % _sourcePool.Count;

        AudioEngine.AL.SourceStop(source);
        AudioEngine.AL.SetSourceProperty(source, SourceFloat.Gain, gain);
        AudioEngine.AL.SetSourceProperty(source, SourceFloat.Pitch, pitch);
        AudioEngine.AL.SourcePlay(source);
    }
}
