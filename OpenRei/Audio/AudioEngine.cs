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
                var device = _alc.OpenDevice(null);
                if (device != null)
                {
                    var context = _alc.CreateContext(device, null);
                    _alc.MakeContextCurrent(context);
                    _isInitialized = true;
                    Console.WriteLine("[OpenRei AudioEngine] OpenAL Soft initialized successfully.");
                }
            }
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
}
