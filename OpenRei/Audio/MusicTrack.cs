using System.Collections.Concurrent;
using Silk.NET.OpenAL;

namespace OpenRei.Audio;

/// <summary>
/// Streaming audio player for music tracks. OpenAL ring buffer filled
/// by a background decode thread. First 2 seconds are filled synchronously
/// so playback starts instantly and reliably.
/// For gameplay requiring sub-ms seek accuracy, call <see cref="ToAudioStreamAsync"/>.
/// </summary>
public sealed unsafe class MusicTrack : IAudioTrack, IDisposable
{
    private const int BufferCount = 4;
    private const double BufferSeconds = 0.5;

    private readonly string _filePath;
    private readonly StreamingDecoder _decoder;
    private readonly uint _sourceId;
    private readonly uint[] _bufferIds;

    private readonly ConcurrentQueue<byte[]> _chunkQueue = new();
    private CancellationTokenSource? _cts;
    private Task? _fillTask;

    private float _volume = 1f;
    private bool _isPlaying;
    private bool _disposed;
    private bool _eofReached;
    private int _queuedCount;
    private int _buffersConsumed; // total buffers played to completion
    private double _seekOffsetMs;
    private double _pausedPositionMs;

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
            if (_isPlaying)
            {
                double baseMs = _seekOffsetMs + _buffersConsumed * BufferSeconds * 1000.0;
                AudioEngine.AL.GetSourceProperty(_sourceId, SourceFloat.SecOffset, out float secOffset);
                return baseMs + secOffset * 1000.0;
            }
            return _pausedPositionMs;
        }
        set => Seek(value);
    }

    public int SampleRate => _decoder.SampleRate;

    // ── Construction ───────────────────────────────────────────────────────────

    public MusicTrack(string path, double startPositionMs = 0)
    {
        _filePath = path;
        if (!AudioEngine.IsInitialized)
            throw new InvalidOperationException("AudioEngine not initialized");

        _decoder = StreamingDecoder.Open(path);
        _seekOffsetMs = startPositionMs;
        _buffersConsumed = 0;

        // Seek before reading any data (reliable startup, no race with fill task)
        if (startPositionMs > 0)
            _decoder.SeekSeconds(startPositionMs / 1000.0);

        _sourceId = AudioEngine.AL.GenSource();
        _bufferIds = new uint[BufferCount];
        for (int i = 0; i < BufferCount; i++)
            _bufferIds[i] = AudioEngine.AL.GenBuffer();

        // Fill all buffers synchronously so playback starts with zero latency
        FillAllBuffersFromDecoder();

        // Background thread keeps the ring buffer full
        _cts = new CancellationTokenSource();
        _fillTask = Task.Run(() => FillLoop(_cts.Token));

        AudioEngine.RegisterMusicTrack(this);
    }

    /// <summary>Reads from decoder and queues all 4 buffers. Called on main thread only.</summary>
    private void FillAllBuffersFromDecoder()
    {
        var format = _decoder.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;
        int bufSize = (int)(SampleRate * 2 * _decoder.Channels * BufferSeconds);

        for (int i = 0; i < BufferCount; i++)
        {
            byte[] chunk = new byte[bufSize];
            int bytes = _decoder.ReadPcm16(chunk, 0, bufSize);
            if (bytes == 0) break;

            fixed (byte* p = chunk)
            {
                AudioEngine.AL.BufferData(_bufferIds[i], format, p, bytes, _decoder.SampleRate);
            }
            fixed (uint* pBuf = &_bufferIds[i])
            {
                AudioEngine.AL.SourceQueueBuffers(_sourceId, 1, pBuf);
            }
            _queuedCount++;
        }
    }

    // ── Playback Control ───────────────────────────────────────────────────────

    public void Play()
    {
        ThrowIfDisposed();
        if (_sourceId == 0 || !AudioEngine.IsInitialized) return;

        Tick();
        AudioEngine.AL.SetSourceProperty(_sourceId, SourceFloat.Gain, _volume);
        AudioEngine.AL.SourcePlay(_sourceId);
        _isPlaying = true;
    }

    public void Pause()
    {
        ThrowIfDisposed();
        if (_sourceId == 0 || !AudioEngine.IsInitialized) return;
        AudioEngine.AL.SourcePause(_sourceId);
        _pausedPositionMs = PositionMs;
        _isPlaying = false;
    }

    public void Stop()
    {
        ThrowIfDisposed();
        if (_sourceId == 0 || !AudioEngine.IsInitialized) return;

        StopFillTask();
        AudioEngine.AL.SourceStop(_sourceId);
        _pausedPositionMs = 0;
        _seekOffsetMs = 0;
        _isPlaying = false;
        _eofReached = false;

        UnqueueAll();
        _decoder.SeekSeconds(0);
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

        UnqueueAll();
        _seekOffsetMs = positionMs;
        _pausedPositionMs = positionMs;
        _buffersConsumed = 0;
        _decoder.SeekSeconds(positionMs / 1000.0);

        // Re-fill all buffers at new position (synchronous)
        FillAllBuffersFromDecoder();

        _cts = new CancellationTokenSource();
        _fillTask = Task.Run(() => FillLoop(_cts.Token));

        if (wasPlaying)
        {
            AudioEngine.AL.SourcePlay(_sourceId);
            _isPlaying = true;
        }
    }

    private void UnqueueAll()
    {
        if (_queuedCount <= 0) return;
        fixed (uint* pBuf = _bufferIds)
        {
            AudioEngine.AL.SourceUnqueueBuffers(_sourceId, _queuedCount, pBuf);
        }
        _queuedCount = 0;
    }

    // ── Internal tick (called from AudioEngine.TickMusicTracks) ────────────────

    internal void Tick()
    {
        if (_sourceId == 0 || !AudioEngine.IsInitialized) return;

        AudioEngine.AL.GetSourceProperty(_sourceId, GetSourceInteger.BuffersProcessed, out int processed);
        AudioEngine.AL.GetSourceProperty(_sourceId, GetSourceInteger.SourceState, out int state);
        var format = _decoder.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;

        // Refill processed buffers from the chunk queue
        while (processed > 0 && _queuedCount > 0)
        {
            uint bufId;
            AudioEngine.AL.SourceUnqueueBuffers(_sourceId, 1, &bufId);
            _queuedCount--;
            _buffersConsumed++;

            if (!_eofReached && _chunkQueue.TryDequeue(out byte[]? chunk))
            {
                fixed (byte* p = chunk)
                {
                    AudioEngine.AL.BufferData(bufId, format, p, chunk.Length, _decoder.SampleRate);
                }
                AudioEngine.AL.SourceQueueBuffers(_sourceId, 1, &bufId);
                _queuedCount++;
            }

            processed--;
        }

        // Restart source if it starved (stopped with no buffers) but we have data
        bool hasBuffers = _queuedCount > 0;
        bool starved = state == 0x1014 && hasBuffers;
        if (_isPlaying && starved)
        {
            AudioEngine.AL.SourcePlay(_sourceId);
        }

        if (_isPlaying && _eofReached && _queuedCount == 0)
        {
            _isPlaying = false;
        }
    }

    // ── Background fill ────────────────────────────────────────────────────────

    private void FillLoop(CancellationToken ct)
    {
        int bufSize = (int)(SampleRate * 2 * _decoder.Channels * BufferSeconds);

        while (!ct.IsCancellationRequested && !_eofReached)
        {
            if (_chunkQueue.Count < BufferCount * 2)
            {
                byte[] chunk = new byte[bufSize];
                int bytes = _decoder.ReadPcm16(chunk, 0, bufSize);
                if (bytes <= 0) { _eofReached = true; break; }
                if (bytes < bufSize) Array.Resize(ref chunk, bytes);
                _chunkQueue.Enqueue(chunk);
            }
            else
            {
                Thread.Sleep(10);
            }
        }
    }

    private void StopFillTask()
    {
        _cts?.Cancel();
        if (_fillTask != null)
        {
            try { _fillTask.Wait(); } catch { /* task was cancelled — ignore */ }
            _fillTask.Dispose();
        }
        _cts?.Dispose();
        _cts = null;
        _fillTask = null;
        while (_chunkQueue.TryDequeue(out _)) { }
    }

    // ── Conversion ─────────────────────────────────────────────────────────────

    public Task<AudioStream> ToAudioStreamAsync() =>
        Task.Run(() => new AudioStream(AudioDecoder.DecodeFile(_filePath)));

    // ── IDisposable ────────────────────────────────────────────────────────────

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MusicTrack));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopFillTask();
        if (AudioEngine.IsInitialized && _sourceId != 0)
        {
            AudioEngine.AL.SourceStop(_sourceId);
            UnqueueAll();
            AudioEngine.AL.DeleteSource(_sourceId);
            foreach (var id in _bufferIds)
                AudioEngine.AL.DeleteBuffer(id);
        }
        _decoder.Dispose();
        AudioEngine.UnregisterMusicTrack(this);
    }
}
