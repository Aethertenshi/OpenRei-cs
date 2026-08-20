namespace reistar.Audios;

using System;
using System.Threading;
using System.Threading.Tasks;
using Silk.NET.OpenAL;

/// <summary>
/// Streaming audio player for music tracks with background decoding, ring-buffered OpenAL queueing,
/// and live sub-millisecond synchronized FFT spectrum extraction.
/// </summary>
public sealed unsafe class MusicTrack : IAudioTrack
{
    private const int BufferCount = 8;
    private const int BufferSizeBytes = 16384;

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

    private readonly short[] _ringBuffer = new short[131072];
    private long _totalSamplesWritten;
    private readonly object _ringLock = new();

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

    public float PlaybackSpeed
    {
        get => Pitch;
        set => Pitch = value;
    }

    public bool Loop
    {
        get => _loop;
        set => _loop = value;
    }

    public bool IsPlaying
    {
        get
        {
            if (!_isPlaying || !AudioEngine.IsInitialized || _sourceId == 0) return false;
            AudioEngine.AL.GetSourceProperty(_sourceId, GetSourceInteger.SourceState, out int state);
            return state == (int)SourceState.Playing;
        }
    }
    public bool IsDisposed => _disposed;
    public Action? OnPlaying { get; set; }

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

        var al = AudioEngine.AL;
        _sourceId = al.GenSource();

        fixed (uint* bPtr = _bufferIds)
        {
            al.GenBuffers(BufferCount, bPtr);
        }

        al.SetSourceProperty(_sourceId, SourceFloat.Gain, _volume);
        al.SetSourceProperty(_sourceId, SourceFloat.Pitch, _pitch);
        al.SetSourceProperty(_sourceId, SourceBoolean.SourceRelative, true);

        if (startPositionMs > 0)
        {
            _decoder.SeekSeconds(startPositionMs / 1000.0);
            _totalSamplesWritten = (long)((startPositionMs / 1000.0) * _decoder.SampleRate) * _decoder.Channels;
        }

        PreloadBuffers();
    }

    private void PushPcmToRing(byte[] pcmBytes, int byteCount)
    {
        int sampleCount = byteCount / 2;
        lock (_ringLock)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
                _ringBuffer[_totalSamplesWritten % _ringBuffer.Length] = sample;
                _totalSamplesWritten++;
            }
        }
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
                PushPcmToRing(tempBuf, read);
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

        var al = AudioEngine.AL;
        al.GetSourceProperty(_sourceId, GetSourceInteger.BuffersQueued, out int queued);
        if (queued == 0)
        {
            PreloadBuffers();
        }

        _isPlaying = true;
        al.SourcePlay(_sourceId);

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
        if (AudioEngine.IsInitialized && _sourceId != 0)
        {
            var al = AudioEngine.AL;
            al.SourceStop(_sourceId);
            al.SetSourceProperty(_sourceId, SourceInteger.Buffer, 0);
        }
    }

    public void Seek(double positionMs)
    {
        bool wasPlaying = _isPlaying;
        Stop();

        _seekOffsetMs = positionMs;
        _totalBytesPlayed = 0;
        _pausedPositionMs = positionMs;
        _decoder.SeekSeconds(positionMs / 1000.0);
        _totalSamplesWritten = (long)((positionMs / 1000.0) * _decoder.SampleRate) * _decoder.Channels;

        var al = AudioEngine.AL;
        al.SourceStop(_sourceId);
        al.SetSourceProperty(_sourceId, SourceInteger.Buffer, 0);

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
        bool eof = false;

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

                int read = 0;
                if (!eof)
                {
                    read = _decoder.ReadPcm16(tempBuf, 0, tempBuf.Length);
                    if (read <= 0)
                    {
                        if (_loop)
                        {
                            _decoder.SeekSeconds(0);
                            _seekOffsetMs = 0;
                            _totalBytesPlayed = 0;
                            _totalSamplesWritten = 0;
                            read = _decoder.ReadPcm16(tempBuf, 0, tempBuf.Length);
                        }
                        else
                        {
                            eof = true;
                        }
                    }
                }

                if (read > 0)
                {
                    PushPcmToRing(tempBuf, read);
                    fixed (byte* ptr = tempBuf)
                    {
                        al.BufferData(buffer, format, ptr, read, _decoder.SampleRate);
                    }
                    al.SourceQueueBuffers(_sourceId, 1, &buffer);
                }
            }

            al.GetSourceProperty(_sourceId, GetSourceInteger.BuffersQueued, out int queued);
            al.GetSourceProperty(_sourceId, GetSourceInteger.SourceState, out int state);

            if (eof && queued == 0 && state != (int)SourceState.Playing)
            {
                _isPlaying = false;
                break;
            }

            if (!eof && state == (int)SourceState.Stopped && queued > 0 && _isPlaying)
            {
                al.SourcePlay(_sourceId);
            }

            OnPlaying?.Invoke();
            Thread.Sleep(10);
        }
    }

    public void GetFftData(Span<float> fftBuffer)
    {
        if (!IsPlaying || _disposed)
        {
            fftBuffer.Clear();
            return;
        }

        int channels = _decoder.Channels;
        int sampleRate = _decoder.SampleRate;
        if (channels <= 0 || sampleRate <= 0)
        {
            fftBuffer.Clear();
            return;
        }

        int fftSize = fftBuffer.Length * 2;
        Span<short> samples = stackalloc short[fftSize];

        double currentSec = Position;
        long currentPlaySample = (long)(currentSec * sampleRate) * channels;

        lock (_ringLock)
        {
            for (int i = 0; i < fftSize; i++)
            {
                long sampleIdx = currentPlaySample + (i * channels);
                if (sampleIdx >= 0 && sampleIdx < _totalSamplesWritten && (_totalSamplesWritten - sampleIdx) < _ringBuffer.Length)
                {
                    samples[i] = _ringBuffer[sampleIdx % _ringBuffer.Length];
                }
                else
                {
                    long fallbackStart = _totalSamplesWritten - (fftSize * channels);
                    if (fallbackStart >= 0)
                    {
                        samples[i] = _ringBuffer[(fallbackStart + i * channels) % _ringBuffer.Length];
                    }
                    else
                    {
                        samples[i] = 0;
                    }
                }
            }
        }

        FftProvider.ComputeSpectrum(samples, fftBuffer);
    }

    public float GetLevel()
    {
        if (!IsPlaying || _disposed) return 0f;

        int channels = _decoder.Channels;
        int sampleRate = _decoder.SampleRate;
        if (channels <= 0 || sampleRate <= 0) return 0f;

        Span<short> samples = stackalloc short[512];
        double currentSec = Position;
        long currentPlaySample = (long)(currentSec * sampleRate) * channels;

        lock (_ringLock)
        {
            for (int i = 0; i < 512; i++)
            {
                long sampleIdx = currentPlaySample + (i * channels);
                if (sampleIdx >= 0 && sampleIdx < _totalSamplesWritten && (_totalSamplesWritten - sampleIdx) < _ringBuffer.Length)
                {
                    samples[i] = _ringBuffer[sampleIdx % _ringBuffer.Length];
                }
                else
                {
                    long fallbackStart = _totalSamplesWritten - (512 * channels);
                    if (fallbackStart >= 0)
                    {
                        samples[i] = _ringBuffer[(fallbackStart + i * channels) % _ringBuffer.Length];
                    }
                    else
                    {
                        samples[i] = 0;
                    }
                }
            }
        }

        return FftProvider.ComputeLevel(samples).Peak;
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
