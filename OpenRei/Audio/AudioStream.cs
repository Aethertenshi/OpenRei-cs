using Silk.NET.OpenAL;

namespace OpenRei.Audio;

public class AudioStream
{
    private uint _sourceId;
    private uint _bufferId;
    private int _sampleRate = 44100;
    private DecodedAudioData? _pendingData;
    private Task<DecodedAudioData?>? _loadTask;
    private bool _playRequested;

    public float Pitch { get; set; } = 1.0f;
    public float Volume { get; set; } = 1.0f;

    public bool IsPlaying
    {
        get
        {
            EnsureHandlesAndUpload();
            if (!AudioEngine.IsInitialized || _sourceId == 0) return false;
            AudioEngine.AL.GetSourceProperty(_sourceId, GetSourceInteger.SourceState, out int state);
            return state == (int)SourceState.Playing;
        }
    }

    public double PositionMs
    {
        get
        {
            EnsureHandlesAndUpload();
            if (!AudioEngine.IsInitialized || _sourceId == 0) return 0.0;

            AudioEngine.AL.GetSourceProperty(_sourceId, SourceFloat.SecOffset, out float secOffset);
            return (double)secOffset * 1000.0;
        }
        set
        {
            EnsureHandlesAndUpload();
            if (!AudioEngine.IsInitialized || _sourceId == 0) return;
            AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.SecOffset, (float)(value / 1000.0));
        }
    }

    public AudioStream()
    {
    }

    public AudioStream(string filePath)
    {
        var data = AudioDecoder.DecodeFile(filePath);
        LoadPcmData(data);
    }

    public AudioStream(DecodedAudioData data)
    {
        LoadPcmData(data);
    }

    public AudioStream(byte[] pcmData, int sampleRate, int channels = 2, int bitsPerSample = 16)
    {
        LoadPcmData(pcmData, sampleRate, channels, bitsPerSample);
    }

    /// <summary>
    /// Creates an AudioStream that decodes asynchronously on a background thread.
    /// Call Play() as usual — it auto-fires once the data is ready.
    /// </summary>
    public static AudioStream CreateAsync(string filePath)
    {
        var stream = new AudioStream();
        stream._loadTask = AudioCache.GetOrDecodeAsync(filePath);
        AudioCache.Track(stream);
        return stream;
    }

    /// <summary>
    /// Checks whether an async load has completed. Called automatically by Play() and
    /// EnsureHandlesAndUpload(). Also called from the main loop via AudioCache.CheckPending().
    /// </summary>
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
                if (_sourceId != 0)
                {
                    AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Pitch, Pitch);
                    AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Gain, Volume);
                    AudioEngine.AL.SourcePlay(_sourceId);
                }
                _playRequested = false;
            }
        }
    }

    public void LoadPcmData(DecodedAudioData data)
    {
        if (data != null)
        {
            _pendingData = data;
            LoadPcmData(data.PcmData, data.SampleRate, data.Channels, data.BitsPerSample);
        }
    }

    public void LoadPcmData(byte[] pcmData, int sampleRate, int channels = 2, int bitsPerSample = 16)
    {
        _sampleRate = sampleRate;
        _pendingData = new DecodedAudioData(pcmData, sampleRate, channels, bitsPerSample, 0f);
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

        if (_sourceId == 0)
        {
            _sourceId = AudioEngine.AL.GenSource();
            _bufferId = AudioEngine.AL.GenBuffer();
        }

        if (_pendingData != null && _pendingData.PcmData.Length > 0)
        {
            var pcmData = _pendingData.PcmData;
            _sampleRate = _pendingData.SampleRate;

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
                    AudioEngine.AL.BufferData(_bufferId, format, ptr, pcmData.Length, _sampleRate);
                }
            }

            AudioEngine.AL.SetSourceProperty(_sourceId, SourceInteger.Buffer, (int)_bufferId);
            _pendingData = null;
        }
    }

    public void Play()
    {
        if (!AudioEngine.IsInitialized) return;

        // Async load still in progress → queue play request for when it finishes
        if (_loadTask != null && !_loadTask.IsCompleted)
        {
            _playRequested = true;
            return;
        }

        // Async load just completed → CheckPendingLoad handles upload + play
        if (_loadTask != null)
        {
            CheckPendingLoad();
            return;
        }

        EnsureHandlesAndUpload();
        if (_sourceId == 0) return;
        AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Pitch, Pitch);
        AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Gain, Volume);
        AudioEngine.AL.SourcePlay(_sourceId);
    }

    public void Pause()
    {
        if (!AudioEngine.IsInitialized || _sourceId == 0) return;
        AudioEngine.AL.SourcePause(_sourceId);
    }

    public void Stop()
    {
        if (!AudioEngine.IsInitialized || _sourceId == 0) return;
        AudioEngine.AL.SourceStop(_sourceId);
    }
}
