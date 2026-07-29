using OpenRei.Tween;
using Silk.NET.OpenAL;

namespace OpenRei.Audio;

/// <summary>
/// Core OpenAL Soft audio device & context manager for sub-millisecond rhythm playback.
/// </summary>
public static class AudioEngine
{
    private static AL? _al;
    private static ALContext? _alc;
    private static bool _isInitialized;

    private static unsafe Device* _device;
    private static unsafe Context* _context;

    public static AL AL => _al ?? throw new InvalidOperationException("AudioEngine is not initialized.");
    public static ALContext ALC => _alc ?? throw new InvalidOperationException("AudioEngine is not initialized.");

    public static bool IsInitialized => _isInitialized;

    public static void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            _alc = ALContext.GetApi();
            _al = AL.GetApi();

            unsafe
            {
                _device = _alc.OpenDevice(null);
                if (_device == null)
                {
                    Console.WriteLine("[OpenRei AudioEngine Warning] OpenAL Soft could not open a default audio device.");
                    return;
                }

                _context = _alc.CreateContext(_device, null);
                if (_context == null)
                {
                    Console.WriteLine("[OpenRei AudioEngine Warning] OpenAL Soft could not create a context.");
                    _alc.CloseDevice(_device);
                    _device = null;
                    return;
                }

                if (!_alc.MakeContextCurrent(_context))
                {
                    Console.WriteLine("[OpenRei AudioEngine Warning] OpenAL Soft could not make context current.");
                    _alc.DestroyContext(_context);
                    _context = null;
                    _alc.CloseDevice(_device);
                    _device = null;
                    return;
                }
            }

            _isInitialized = true;
            Console.WriteLine("[OpenRei AudioEngine] OpenAL Soft initialized successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenRei AudioEngine Warning] OpenAL Soft native library not loaded: {ex.Message}");
        }
    }

    public static void SetMasterVolume(float volume)
    {
        if (!_isInitialized) return;
        _al?.SetListenerProperty(ListenerFloat.Gain, Math.Clamp(volume, 0f, 1f));
    }

    public static void Shutdown()
    {
        if (!_isInitialized) return;

        unsafe
        {
            if (_context != null)
            {
                _alc!.MakeContextCurrent(null);
                _alc.DestroyContext(_context);
                _context = null;
            }

            if (_device != null)
            {
                _alc!.CloseDevice(_device);
                _device = null;
            }
        }

        _isInitialized = false;
        Console.WriteLine("[OpenRei AudioEngine] OpenAL Soft shut down.");
    }

    // ── Crossfade ──────────────────────────────────────────────────────────────

    private static OpenRei.Tween.Tween? _fadeInTween;

    /// <summary>
    /// Smoothly transitions between two audio streams over the given duration.
    /// Fade-in uses ease-In (accelerating), fade-out uses ease-Out (decelerating to 0).
    /// Handles rapid re-calls: previous fade-in is stopped so its target stream
    /// becomes the new old-stream at its current volume.
    /// </summary>
    public static void Crossfade(
        IAudioTrack? oldStream,
        IAudioTrack? newStream,
        float duration,
        Easing easing = Easing.Quadratic,
        EasingDirection direction = EasingDirection.Out)
    {
        if (!_isInitialized || newStream == null || newStream == oldStream)
            return;

        // Stop previous fade-in so its target volume is frozen
        _fadeInTween?.Stop();
        _fadeInTween = null;

        if (duration <= 0f)
        {
            // Instant snap
            if (oldStream != null) { oldStream.Volume = 0f; oldStream.Stop(); }
            newStream.Volume = 0f;
            if (!newStream.IsPlaying) newStream.Play();
            newStream.Volume = 1f;
            return;
        }

        newStream.Volume = 0f;
        if (!newStream.IsPlaying)
            newStream.Play();

        // Fade new in: 0 → 1 (ease-in)
        _fadeInTween = new OpenRei.Tween.Tween(0f, 1f, duration,
            v => newStream.Volume = v,
            easing, EasingDirection.In);
        _fadeInTween.Start();

        // Fade old out: current → 0 (ease-out), stop on complete
        if (oldStream != null && oldStream != newStream)
        {
            float startVol = oldStream.Volume;
            new OpenRei.Tween.Tween(startVol, 0f, duration,
                v => oldStream.Volume = v,
                easing, EasingDirection.Out,
                onComplete: () => { oldStream.Stop(); oldStream.Volume = 0f; }).Start();
        }
    }

    // ── MusicTrack Registry ────────────────────────────────────────────────────

    private static readonly List<WeakReference<MusicTrack>> _musicTracks = new();

    internal static void RegisterMusicTrack(MusicTrack track)
    {
        lock (_musicTracks)
            _musicTracks.Add(new WeakReference<MusicTrack>(track));
    }

    internal static void UnregisterMusicTrack(MusicTrack track)
    {
        lock (_musicTracks)
        {
            for (int i = _musicTracks.Count - 1; i >= 0; i--)
            {
                if (_musicTracks[i].TryGetTarget(out var t) && t == track)
                {
                    _musicTracks.RemoveAt(i);
                    return;
                }
            }
        }
    }

    /// <summary>Call every frame from the main loop. Refills streaming buffers.</summary>
    internal static void TickMusicTracks()
    {
        lock (_musicTracks)
        {
            for (int i = _musicTracks.Count - 1; i >= 0; i--)
            {
                if (_musicTracks[i].TryGetTarget(out var track))
                    track.Tick();
                else
                    _musicTracks.RemoveAt(i);
            }
        }
    }
}
