using Silk.NET.OpenAL;

namespace OpenRei.Audio;

/// <summary>
/// Handles rhythm music stream playback with millisecond sample-accurate position querying.
/// </summary>
public class AudioStream
{
    private uint _sourceId;
    private uint _bufferId;
    private int _sampleRate = 44100;
    private bool _isPlaying;
    private DecodedAudioData? _pendingData;

    public float Pitch { get; set; } = 1.0f;
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Indicates whether the audio stream is currently playing.
    /// </summary>
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

    /// <summary>
    /// Returns the sub-millisecond audio hardware playback position for rhythm timing.
    /// </summary>
    public double PositionMs
    {
        get
        {
            EnsureHandlesAndUpload();
            if (!_isPlaying || !AudioEngine.IsInitialized || _sourceId == 0) return 0.0;

            AudioEngine.AL.GetSourceProperty(_sourceId, GetSourceInteger.SampleOffset, out int sampleOffset);
            return (double)sampleOffset / _sampleRate * 1000.0;
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
            _pendingData = null; // Upload complete
        }
    }

    public void Play()
    {
        EnsureHandlesAndUpload();
        if (!AudioEngine.IsInitialized || _sourceId == 0) return;
        AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Pitch, Pitch);
        AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Gain, Volume);
        AudioEngine.AL.SourcePlay(_sourceId);
        _isPlaying = true;
    }

    public void Pause()
    {
        if (!AudioEngine.IsInitialized || _sourceId == 0) return;
        AudioEngine.AL.SourcePause(_sourceId);
        _isPlaying = false;
    }

    public void Stop()
    {
        if (!AudioEngine.IsInitialized || _sourceId == 0) return;
        AudioEngine.AL.SourceStop(_sourceId);
        _isPlaying = false;
    }
}
