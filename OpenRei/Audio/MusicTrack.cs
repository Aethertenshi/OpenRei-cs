using System.Collections.Concurrent;
using System.Collections.Generic;
using Silk.NET.OpenAL;

namespace OpenRei.Audio;

/// <summary>
/// Streaming audio player for music tracks. Uses a robust ring buffer of OpenAL sources
/// filled by a background decode thread. Pre-buffers 4 OpenAL targets to eliminate stuttering and noise artifacts.
/// </summary>
public sealed unsafe class MusicTrack : IAudioTrack, IDisposable
{
    private const int BufferCount = 4;
    private const double BufferSeconds = 0.25; // 250ms per chunk = 1.0s total pre-buffered audio

    private readonly string _filePath;
    private readonly StreamingDecoder _decoder;
    private readonly uint _sourceId;
    private readonly uint[] _bufferIds;
    private readonly Queue<uint> _freeBuffers = new();

    private readonly ConcurrentQueue<byte[]> _chunkQueue = new();
    private CancellationTokenSource? _cts;
    private Task? _fillTask;

    private float _volume = 1f;
    private bool _isPlaying;
    private bool _disposed;
    private bool _eofReached;

    private int _queuedCount;
    private bool _startedOnce;

    // ── Properties ─────────────────────────────────────────────────────────────

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (AudioEngine.IsInitialized && _sourceId != 0)
                AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Gain, _volume);
        }
    }

    public bool IsPlaying => _isPlaying;
    public double LengthMs => _decoder.LengthSeconds * 1000.0;

    public double PositionMs
    {
        get
        {
            if (!AudioEngine.IsInitialized || _sourceId == 0) return _decoder.PositionSeconds * 1000.0;

            if (_isPlaying)
            {
                AudioEngine.AL.GetSourceProperty(_sourceId, SourceFloat.SecOffset, out float secOffset);
                return secOffset * 1000.0;
            }
            return _decoder.PositionSeconds * 1000.0;
        }
        set => Seek(value);
    }

    public int SampleRate => _decoder.SampleRate;

    // ── Construction ───────────────────────────────────────────────────────────

    public MusicTrack(string path)
    {
        _filePath = path;

        if (!AudioEngine.IsInitialized)
            throw new InvalidOperationException("AudioEngine not initialized");

        _decoder = StreamingDecoder.Open(path);

        _sourceId = AudioEngine.AL.GenSource();
        _bufferIds = new uint[BufferCount];
        for (int i = 0; i < BufferCount; i++)
        {
            uint id = AudioEngine.AL.GenBuffer();
            _bufferIds[i] = id;
            _freeBuffers.Enqueue(id);
        }

        // Start background decode thread immediately
        _cts = new CancellationTokenSource();
        _fillTask = Task.Run(() => FillLoop(_cts.Token));

        // Pre-fill OpenAL buffer ring synchronously with first chunks
        PreFillOpenALBuffers();

        AudioEngine.RegisterMusicTrack(this);
    }

    private void PreFillOpenALBuffers()
    {
        int frameSize = _decoder.Channels * 2;
        int bufSize = (int)(SampleRate * frameSize * BufferSeconds);
        bufSize = (bufSize / frameSize) * frameSize;

        var format = _decoder.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;

        while (_freeBuffers.Count > 0)
        {
            byte[] chunk = new byte[bufSize];
            int bytes = _decoder.ReadPcm16(chunk, 0, bufSize);
            if (bytes <= 0)
            {
                _eofReached = true;
                break;
            }

            uint bufId = _freeBuffers.Dequeue();
            fixed (byte* p = chunk)
            {
                AudioEngine.AL.BufferData(bufId, format, p, bytes, _decoder.SampleRate);
            }
            AudioEngine.AL.SourceQueueBuffers(_sourceId, 1, &bufId);
            _queuedCount++;
            _startedOnce = true;
        }
    }

    // ── Playback Control ───────────────────────────────────────────────────────

    public void Play()
    {
        ThrowIfDisposed();
        if (_sourceId == 0 || !AudioEngine.IsInitialized) return;

        Tick();
        AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Gain, _volume);

        if (!_startedOnce)
        {
            PreFillOpenALBuffers();
        }

        AudioEngine.AL.SourcePlay(_sourceId);
        _isPlaying = true;
    }

    public void Pause()
    {
        ThrowIfDisposed();
        if (_sourceId == 0 || !AudioEngine.IsInitialized) return;
        AudioEngine.AL.SourcePause(_sourceId);
        _isPlaying = false;
    }

    public void Stop()
    {
        ThrowIfDisposed();
        if (_sourceId == 0 || !AudioEngine.IsInitialized) return;

        StopFillTask();
        AudioEngine.AL.SourceStop(_sourceId);
        _isPlaying = false;
        _eofReached = false;

        UnqueueAllBuffers();
        _startedOnce = false;

        // Re-seek decoder for next Play()
        _decoder.SeekSeconds(0);

        _cts = new CancellationTokenSource();
        _fillTask = Task.Run(() => FillLoop(_cts.Token));
        PreFillOpenALBuffers();
    }

    public void Seek(double positionMs)
    {
        ThrowIfDisposed();
        if (_sourceId == 0 || !AudioEngine.IsInitialized) return;

        bool wasPlaying = _isPlaying;

        StopFillTask();
        AudioEngine.AL.SourceStop(_sourceId);
        _isPlaying = false;
        _eofReached = false;

        UnqueueAllBuffers();

        _decoder.SeekSeconds(positionMs / 1000.0);

        _cts = new CancellationTokenSource();
        _fillTask = Task.Run(() => FillLoop(_cts.Token));
        PreFillOpenALBuffers();

        if (wasPlaying)
        {
            AudioEngine.AL.SourcePlay(_sourceId);
            _isPlaying = true;
        }
    }

    private void UnqueueAllBuffers()
    {
        if (_queuedCount > 0)
        {
            var unqueued = new uint[_queuedCount];
            fixed (uint* pBuf = unqueued)
            {
                AudioEngine.AL.SourceUnqueueBuffers(_sourceId, _queuedCount, pBuf);
            }
            foreach (var id in unqueued)
            {
                if (id != 0 && !_freeBuffers.Contains(id))
                    _freeBuffers.Enqueue(id);
            }
            _queuedCount = 0;
        }

        while (_freeBuffers.Count < BufferCount)
        {
            foreach (var id in _bufferIds)
            {
                if (!_freeBuffers.Contains(id))
                    _freeBuffers.Enqueue(id);
            }
        }
    }

    // ── Internal (called from AudioEngine.TickMusicTracks) ─────────────────────

    internal void Tick()
    {
        if (_sourceId == 0 || !AudioEngine.IsInitialized) return;

        AudioEngine.AL.GetSourceProperty(_sourceId, GetSourceInteger.BuffersProcessed, out int processed);
        AudioEngine.AL.GetSourceProperty(_sourceId, GetSourceInteger.SourceState, out int state);

        // 1. Return processed OpenAL buffers to free buffer queue
        while (processed > 0 && _queuedCount > 0)
        {
            uint bufId = 0;
            AudioEngine.AL.SourceUnqueueBuffers(_sourceId, 1, &bufId);
            _queuedCount--;

            if (bufId != 0)
            {
                _freeBuffers.Enqueue(bufId);
            }
            processed--;
        }

        // 2. Fill free OpenAL buffers from chunk queue
        var format = _decoder.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;
        while (_freeBuffers.Count > 0 && _chunkQueue.TryDequeue(out byte[]? chunk))
        {
            uint bufId = _freeBuffers.Dequeue();
            fixed (byte* p = chunk)
            {
                AudioEngine.AL.BufferData(bufId, format, p, chunk.Length, _decoder.SampleRate);
            }
            AudioEngine.AL.SourceQueueBuffers(_sourceId, 1, &bufId);
            _queuedCount++;
        }

        // 3. Restart playback seamlessly if source starved or paused unexpectedly while active
        if (_isPlaying && _queuedCount > 0 && (state == 0x1014 || state == 0x1012))
        {
            AudioEngine.AL.SourcePlay(_sourceId);
        }

        // 4. EOF check
        if (_isPlaying && _eofReached && _queuedCount == 0 && _chunkQueue.IsEmpty)
        {
            _isPlaying = false;
        }
    }

    // ── Background fill ────────────────────────────────────────────────────────

    private void FillLoop(CancellationToken ct)
    {
        int frameSize = _decoder.Channels * 2;
        int bufSize = (int)(SampleRate * frameSize * BufferSeconds);
        bufSize = (bufSize / frameSize) * frameSize;

        while (!ct.IsCancellationRequested && !_eofReached)
        {
            if (_chunkQueue.Count < BufferCount * 3)
            {
                byte[] chunk = new byte[bufSize];
                int bytes = _decoder.ReadPcm16(chunk, 0, bufSize);

                if (bytes <= 0)
                {
                    _eofReached = true;
                    break;
                }

                if (bytes < bufSize)
                {
                    Array.Resize(ref chunk, bytes);
                }

                _chunkQueue.Enqueue(chunk);
            }
            else
            {
                Thread.Sleep(5);
            }
        }
    }

    private void StopFillTask()
    {
        _cts?.Cancel();
        _fillTask?.Wait(100);
        _cts?.Dispose();
        _cts = null;
        _fillTask = null;

        while (_chunkQueue.TryDequeue(out _)) { }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MusicTrack));
    }

    // ── Conversion ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Decodes the full track into a PCM AudioStream for gameplay use.
    /// Runs on ThreadPool — non-blocking. MusicTrack continues playing unaffected.
    /// </summary>
    public Task<AudioStream> ToAudioStreamAsync()
    {
        return Task.Run(() =>
        {
            var data = AudioDecoder.DecodeFile(_filePath);
            return new AudioStream(data);
        });
    }

    // ── IDisposable ────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopFillTask();

        if (AudioEngine.IsInitialized && _sourceId != 0)
        {
            AudioEngine.AL.SourceStop(_sourceId);
            UnqueueAllBuffers();

            AudioEngine.AL.DeleteSource(_sourceId);
            foreach (var id in _bufferIds)
                AudioEngine.AL.DeleteBuffer(id);
        }

        _decoder.Dispose();
        AudioEngine.UnregisterMusicTrack(this);
    }
}
