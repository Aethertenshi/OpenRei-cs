namespace reistar.Audios;

using System;
using Silk.NET.OpenAL;

/// <summary>
/// Full-PCM audio stream player with sub-millisecond seek accuracy and hardware pitch scaling.
/// </summary>
public class AudioStream : IAudioTrack
{
    private uint _sourceId;
    private uint _bufferId;
    private int _sampleRate = 44100;
    private int _channels = 2;
    private byte[] _pcmData = Array.Empty<byte>();
    private double _durationSeconds;
    private float _volume = 1.0f;
    private float _pitch = 1.0f;
    private bool _loop = false;
    private bool _disposed;

    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = Math.Clamp(value, 0.1f, 4.0f);
            if (AudioEngine.IsInitialized && _sourceId != 0)
            {
                AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Pitch, _pitch);
            }
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (AudioEngine.IsInitialized && _sourceId != 0)
            {
                AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Gain, _volume);
            }
        }
    }

    public bool Loop
    {
        get => _loop;
        set
        {
            _loop = value;
            if (AudioEngine.IsInitialized && _sourceId != 0)
            {
                AudioEngine.AL.SetSourceProperty(_sourceId, SourceBoolean.Looping, _loop);
            }
        }
    }

    public float PlaybackSpeed
    {
        get => Pitch;
        set => Pitch = value;
    }

    public bool IsPlaying
    {
        get
        {
            if (!AudioEngine.IsInitialized || _sourceId == 0) return false;
            AudioEngine.AL.GetSourceProperty(_sourceId, GetSourceInteger.SourceState, out int state);
            return state == (int)SourceState.Playing;
        }
    }

    public bool IsDisposed => _disposed;
    public Action? OnPlaying { get; set; }

    public double Position
    {
        get => PositionMs / 1000.0;
        set => PositionMs = value * 1000.0;
    }

    public double PositionMs
    {
        get
        {
            if (!AudioEngine.IsInitialized || _sourceId == 0) return 0.0;
            AudioEngine.AL.GetSourceProperty(_sourceId, SourceFloat.SecOffset, out float secOffset);
            return (double)secOffset * 1000.0;
        }
        set => Seek(value);
    }

    public double Length => _durationSeconds;
    public double LengthMs => _durationSeconds * 1000.0;

    public AudioStream(string filePath)
    {
        AudioEngine.Initialize();
        var data = AudioDecoder.DecodeFile(filePath);
        LoadData(data);
    }

    public AudioStream(DecodedAudioData data)
    {
        AudioEngine.Initialize();
        LoadData(data);
    }

    private void LoadData(DecodedAudioData data)
    {
        if (!AudioEngine.IsInitialized || data.PcmData.Length == 0) return;

        _sampleRate = data.SampleRate;
        _channels = data.Channels;
        _pcmData = data.PcmData;
        _durationSeconds = data.DurationSeconds;

        var al = AudioEngine.AL;
        _bufferId = al.GenBuffer();
        _sourceId = al.GenSource();

        BufferFormat format = data.Channels == 1
            ? (data.BitsPerSample == 8 ? BufferFormat.Mono8 : BufferFormat.Mono16)
            : (data.BitsPerSample == 8 ? BufferFormat.Stereo8 : BufferFormat.Stereo16);

        unsafe
        {
            fixed (byte* ptr = data.PcmData)
            {
                al.BufferData(_bufferId, format, ptr, data.PcmData.Length, data.SampleRate);
            }
        }

        al.SetSourceProperty(_sourceId, SourceInteger.Buffer, (int)_bufferId);
        al.SetSourceProperty(_sourceId, SourceFloat.Gain, _volume);
        al.SetSourceProperty(_sourceId, SourceFloat.Pitch, _pitch);
        al.SetSourceProperty(_sourceId, SourceBoolean.Looping, _loop);
        al.SetSourceProperty(_sourceId, SourceBoolean.SourceRelative, true);
    }

    public void Play()
    {
        if (!AudioEngine.IsInitialized || _sourceId == 0) return;
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

    public void Seek(double positionMs)
    {
        if (!AudioEngine.IsInitialized || _sourceId == 0) return;
        float secOffset = (float)Math.Clamp(positionMs / 1000.0, 0.0, _durationSeconds);
        AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.SecOffset, secOffset);
    }

    public void GetFftData(Span<float> fftBuffer)
    {
        if (!IsPlaying || _disposed || _pcmData.Length == 0)
        {
            fftBuffer.Clear();
            return;
        }

        int fftSize = fftBuffer.Length * 2;
        Span<short> samples = stackalloc short[fftSize];

        double currentSec = Position;
        int currentSampleIdx = (int)(currentSec * _sampleRate) * _channels;
        int totalSamples = _pcmData.Length / 2;

        for (int i = 0; i < fftSize; i++)
        {
            int idx = currentSampleIdx + (i * _channels);
            if (idx >= 0 && idx + 1 < totalSamples)
            {
                int byteIdx = idx * 2;
                samples[i] = (short)(_pcmData[byteIdx] | (_pcmData[byteIdx + 1] << 8));
            }
            else
            {
                samples[i] = 0;
            }
        }

        FftProvider.ComputeSpectrum(samples, fftBuffer);
    }

    public float GetLevel()
    {
        if (!IsPlaying || _disposed || _pcmData.Length == 0) return 0f;

        Span<short> samples = stackalloc short[512];
        double currentSec = Position;
        int currentSampleIdx = (int)(currentSec * _sampleRate) * _channels;
        int totalSamples = _pcmData.Length / 2;

        for (int i = 0; i < 512; i++)
        {
            int idx = currentSampleIdx + (i * _channels);
            if (idx >= 0 && idx + 1 < totalSamples)
            {
                int byteIdx = idx * 2;
                samples[i] = (short)(_pcmData[byteIdx] | (_pcmData[byteIdx + 1] << 8));
            }
            else
            {
                samples[i] = 0;
            }
        }

        return FftProvider.ComputeLevel(samples).Peak;
    }

    public void Dispose()
    {
        if (!AudioEngine.IsInitialized) return;
        var al = AudioEngine.AL;

        if (_sourceId != 0)
        {
            al.SourceStop(_sourceId);
            al.DeleteSource(_sourceId);
            _sourceId = 0;
        }

        if (_bufferId != 0)
        {
            al.DeleteBuffer(_bufferId);
            _bufferId = 0;
        }
        _disposed = true;
    }
}
