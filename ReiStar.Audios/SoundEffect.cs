namespace reistar.Audios;

using System;
using System.Collections.Generic;
using Silk.NET.OpenAL;

/// <summary>
/// Pre-buffered low-latency sound effect player for instant hitsounds and UI feedback.
/// Uses a pool of OpenAL sources for zero-latency polyphony.
/// </summary>
public class SoundEffect : IDisposable
{
    private uint _bufferId;
    private readonly List<uint> _sourcePool = new();
    private int _poolIndex;
    private float _volume = 1f;

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (!AudioEngine.IsInitialized) return;
            foreach (var source in _sourcePool)
            {
                AudioEngine.AL.SetSourceProperty(source, SourceFloat.Gain, _volume);
            }
        }
    }

    public bool IsPlaying
    {
        get
        {
            if (!AudioEngine.IsInitialized || _sourcePool.Count == 0) return false;
            foreach (var source in _sourcePool)
            {
                AudioEngine.AL.GetSourceProperty(source, GetSourceInteger.SourceState, out int state);
                if (state == (int)SourceState.Playing) return true;
            }
            return false;
        }
    }

    public SoundEffect(string filePath, int poolSize = 8)
    {
        var data = AudioDecoder.DecodeFile(filePath);
        LoadPcmData(data, poolSize);
    }

    public SoundEffect(DecodedAudioData data, int poolSize = 8)
    {
        LoadPcmData(data, poolSize);
    }

    private void LoadPcmData(DecodedAudioData data, int poolSize)
    {
        AudioEngine.Initialize();
        if (!AudioEngine.IsInitialized || data.PcmData.Length == 0) return;

        var al = AudioEngine.AL;
        _bufferId = al.GenBuffer();

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

        int count = Math.Max(1, poolSize);
        for (int i = 0; i < count; i++)
        {
            uint source = al.GenSource();
            al.SetSourceProperty(source, SourceInteger.Buffer, (int)_bufferId);
            al.SetSourceProperty(source, SourceFloat.Gain, _volume);
            al.SetSourceProperty(source, SourceBoolean.SourceRelative, true);
            _sourcePool.Add(source);
        }
    }

    public void Play(float volume = 1f, float pitch = 1f)
    {
        if (!AudioEngine.IsInitialized || _sourcePool.Count == 0) return;

        var al = AudioEngine.AL;
        uint source = _sourcePool[_poolIndex];
        _poolIndex = (_poolIndex + 1) % _sourcePool.Count;

        al.SetSourceProperty(source, SourceFloat.Gain, Math.Clamp(volume * _volume, 0f, 1f));
        al.SetSourceProperty(source, SourceFloat.Pitch, Math.Clamp(pitch, 0.1f, 4f));
        al.SourceStop(source);
        al.SourcePlay(source);
    }

    public void Stop()
    {
        if (!AudioEngine.IsInitialized || _sourcePool.Count == 0) return;
        var al = AudioEngine.AL;
        foreach (var source in _sourcePool)
        {
            al.SourceStop(source);
        }
    }

    public void Dispose()
    {
        if (!AudioEngine.IsInitialized) return;
        var al = AudioEngine.AL;
        foreach (var source in _sourcePool)
        {
            al.SourceStop(source);
            al.DeleteSource(source);
        }
        _sourcePool.Clear();

        if (_bufferId != 0)
        {
            al.DeleteBuffer(_bufferId);
            _bufferId = 0;
        }
    }
}
