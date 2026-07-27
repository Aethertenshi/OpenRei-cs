using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Controls SDL3_ttf font subsystem initialization and global font management.
/// </summary>
public static class FontEngine
{
    private static bool _isInitialized;

    public static bool IsInitialized => _isInitialized;

    public static void Initialize()
    {
        if (_isInitialized) return;

        if (SDL3_ttf.TTF_Init())
        {
            _isInitialized = true;
            Console.WriteLine("[OpenRei FontEngine] SDL3_ttf initialized successfully.");
        }
        else
        {
            Console.WriteLine($"[SDL3_ttf Warning] Could not initialize font engine: {SDL3.SDL_GetError()}");
        }
    }

    public static void Shutdown()
    {
        if (_isInitialized)
        {
            SDL3_ttf.TTF_Quit();
            _isInitialized = false;
        }
    }
}
