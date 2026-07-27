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
}
