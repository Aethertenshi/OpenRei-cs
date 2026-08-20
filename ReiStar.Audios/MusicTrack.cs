namespace reistar.Audios;

using System;
using System.Threading;
using System.Threading.Tasks;
using Silk.NET.OpenAL;

/// <summary>
/// Streaming audio player for music tracks with background decoding and ring-buffered OpenAL queueing.
/// </summary>
public sealed unsafe class MusicTrack : IAudioTrack
{
    private const int BufferCount = 4;
    private const int BufferSizeBytes = 65536;

    private readonly string _filePath;
    private readonly StreamingDecoder _decoder;
    private readonly uint _sourceId;
    private readonly uint[] _bufferIds = new uint[BufferCount];

    private CancellationTokenSource? _cts;
    private Task? _streamTask;

    private float _volume = 1f;
    private float _pitch = 1f;
    private bool _loop = false;
    private bool _isPlaying;
    private bool _disposed;
    private double _seekOffsetMs;
    private double _pausedPositionMs;
    private long _totalBytesPlayed;

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

    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = Math.Clamp(value, 0.1f, 4f);
            if (AudioEngine.IsInitialized && _sourceId != 0)
            {
                AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Pitch, _pitch);
            }
        }
    }

    public bool Loop
    {
        get => _loop;
        set => _loop = value;
    }

    public bool IsPlaying => _isPlaying;

    public double Length => _decoder.LengthSeconds;
    public double LengthMs => _decoder.LengthSeconds * 1000.0;

    public double Position
    {
        get => PositionMs / 1000.0;
        set => PositionMs = value * 1000.0;
    }

    public double PositionMs
    {
        get
        {
            if (_isPlaying && AudioEngine.IsInitialized && _sourceId != 0)
            {
                AudioEngine.AL.GetSourceProperty(_sourceId, SourceFloat.SecOffset, out float secOffset);
                double byteTimeMs = (_totalBytesPlayed * 1000.0) / (_decoder.SampleRate * _decoder.Channels * 2);
                return _seekOffsetMs + byteTimeMs + (secOffset * 1000.0);
            }
            return _pausedPositionMs;
        }
        set => Seek(value);
    }

    public MusicTrack(string path, double startPositionMs = 0)
    {
        _filePath = path;
        AudioEngine.Initialize();
        if (!AudioEngine.IsInitialized)
            throw new InvalidOperationException("AudioEngine not initialized.");

        _decoder = StreamingDecoder.Open(path);
        _seekOffsetMs = startPositionMs;

        if (startPositionMs > 0)
        {
            _decoder.SeekSeconds(startPositionMs / 1000.0);
        }

        var al = AudioEngine.AL;
        _sourceId = al.GenSource();
        al.SetSourceProperty(_sourceId, SourceFloat.Gain, _volume);
        al.SetSourceProperty(_sourceId, SourceFloat.Pitch, _pitch);
        al.SetSourceProperty(_sourceId, SourceBoolean.SourceRelative, true);

        for (int i = 0; i < BufferCount; i++)
        {
            _bufferIds[i] = al.GenBuffer();
        }

        PreloadBuffers();
    }

    private void PreloadBuffers()
    {
        var al = AudioEngine.AL;
        byte[] tempBuf = new byte[BufferSizeBytes];

        BufferFormat format = _decoder.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;

        for (int i = 0; i < BufferCount; i++)
        {
            int read = _decoder.ReadPcm16(tempBuf, 0, tempBuf.Length);
            if (read > 0)
            {
                fixed (byte* ptr = tempBuf)
                {
                    al.BufferData(_bufferIds[i], format, ptr, read, _decoder.SampleRate);
                }
                fixed (uint* bPtr = &_bufferIds[i])
                {
                    al.SourceQueueBuffers(_sourceId, 1, bPtr);
                }
            }
        }
    }

    public void Play()
    {
        if (_isPlaying || _disposed) return;

        _isPlaying = true;
        AudioEngine.AL.SourcePlay(_sourceId);

        _cts = new CancellationTokenSource();
        _streamTask = Task.Run(() => StreamLoop(_cts.Token));
    }

    public void Pause()
    {
        if (!_isPlaying || _disposed) return;

        _pausedPositionMs = PositionMs;
        _isPlaying = false;
        _cts?.Cancel();
        AudioEngine.AL.SourcePause(_sourceId);
    }

    public void Stop()
    {
        if (_disposed) return;

        _isPlaying = false;
        _cts?.Cancel();
        _pausedPositionMs = 0;
        AudioEngine.AL.SourceStop(_sourceId);
    }

    public void Seek(double positionMs)
    {
        bool wasPlaying = _isPlaying;
        Stop();

        _seekOffsetMs = positionMs;
        _totalBytesPlayed = 0;
        _pausedPositionMs = positionMs;
        _decoder.SeekSeconds(positionMs / 1000.0);

        // Unqueue all buffers
        var al = AudioEngine.AL;
        al.GetSourceProperty(_sourceId, GetSourceInteger.BuffersQueued, out int queued);
        while (queued > 0)
        {
            uint unqueued = 0;
            al.SourceUnqueueBuffers(_sourceId, 1, &unqueued);
            queued--;
        }

        PreloadBuffers();

        if (wasPlaying)
        {
            Play();
        }
    }

    private void StreamLoop(CancellationToken token)
    {
        byte[] tempBuf = new byte[BufferSizeBytes];
        BufferFormat format = _decoder.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;
        var al = AudioEngine.AL;

        while (!token.IsCancellationRequested && _isPlaying)
        {
            al.GetSourceProperty(_sourceId, GetSourceInteger.BuffersProcessed, out int processed);

            while (processed > 0 && !token.IsCancellationRequested)
            {
                uint buffer = 0;
                al.SourceUnqueueBuffers(_sourceId, 1, &buffer);
                processed--;

                al.GetBufferProperty(buffer, GetBufferInteger.Size, out int bufferSize);
                _totalBytesPlayed += bufferSize;

                int read = _decoder.ReadPcm16(tempBuf, 0, tempBuf.Length);
                if (read <= 0 && _loop)
                {
                    _decoder.SeekSeconds(0);
                    _seekOffsetMs = 0;
                    _totalBytesPlayed = 0;
                    read = _decoder.ReadPcm16(tempBuf, 0, tempBuf.Length);
                }

                if (read > 0)
                {
                    fixed (byte* ptr = tempBuf)
                    {
                        al.BufferData(buffer, format, ptr, read, _decoder.SampleRate);
                    }
                    al.SourceQueueBuffers(_sourceId, 1, &buffer);
                }
            }

            // If playback starved, restart it
            al.GetSourceProperty(_sourceId, GetSourceInteger.SourceState, out int state);
            if (state == (int)SourceState.Stopped && _isPlaying)
            {
                al.SourcePlay(_sourceId);
            }

            Thread.Sleep(15);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _isPlaying = false;
        _cts?.Cancel();
        _cts?.Dispose();

        if (AudioEngine.IsInitialized)
        {
            var al = AudioEngine.AL;
            al.SourceStop(_sourceId);
            al.DeleteSource(_sourceId);

            for (int i = 0; i < BufferCount; i++)
            {
                if (_bufferIds[i] != 0)
                {
                    al.DeleteBuffer(_bufferIds[i]);
                }
            }
        }

        _decoder.Dispose();
    }
}
