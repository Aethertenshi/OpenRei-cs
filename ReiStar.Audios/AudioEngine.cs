namespace reistar.Audios;

using System;
using Silk.NET.OpenAL;

/// <summary>
/// Core OpenAL Soft audio device & context manager for sub-millisecond rhythm playback.
/// </summary>
public static unsafe class AudioEngine
{
    private static AL? _al;
    private static ALContext? _alc;
    private static bool _isInitialized;

    private static Device* _device;
    private static Context* _context;
    private static float _masterVolume = 1.0f;

    public static AL AL => _al ?? throw new InvalidOperationException("AudioEngine is not initialized.");
    public static ALContext ALC => _alc ?? throw new InvalidOperationException("AudioEngine is not initialized.");

    public static bool IsInitialized => _isInitialized;

    public static float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0f, 1f);
            if (_isInitialized && _al != null)
            {
                _al.SetListenerProperty(ListenerFloat.Gain, _masterVolume);
            }
        }
    }

    public static void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            _alc = ALContext.GetApi();
            _al = AL.GetApi();

            _device = _alc.OpenDevice(null);
            if (_device == null)
            {
                Console.WriteLine("[ReiStar AudioEngine Warning] OpenAL Soft could not open default audio device.");
                return;
            }

            _context = _alc.CreateContext(_device, null);
            if (_context == null)
            {
                Console.WriteLine("[ReiStar AudioEngine Warning] OpenAL Soft could not create a context.");
                _alc.CloseDevice(_device);
                _device = null;
                return;
            }

            if (!_alc.MakeContextCurrent(_context))
            {
                Console.WriteLine("[ReiStar AudioEngine Warning] OpenAL Soft could not make context current.");
                _alc.DestroyContext(_context);
                _alc.CloseDevice(_device);
                _context = null;
                _device = null;
                return;
            }

            _al.SetListenerProperty(ListenerFloat.Gain, _masterVolume);
            _isInitialized = true;
            Console.WriteLine("[ReiStar AudioEngine] OpenAL Soft audio device initialized successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReiStar AudioEngine Error] Initialization failed: {ex.Message}");
        }
    }

    public static void Shutdown()
    {
        if (!_isInitialized) return;

        try
        {
            if (_alc != null)
            {
                _alc.MakeContextCurrent(null);
                if (_context != null)
                {
                    _alc.DestroyContext(_context);
                    _context = null;
                }
                if (_device != null)
                {
                    _alc.CloseDevice(_device);
                    _device = null;
                }
            }

            _al?.Dispose();
            _alc?.Dispose();
            _al = null;
            _alc = null;
            _isInitialized = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReiStar AudioEngine Error] Shutdown error: {ex.Message}");
        }
    }
}
