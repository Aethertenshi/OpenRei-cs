using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Controls SDL3_ttf font subsystem initialization and global font management.
/// </summary>
public static class FontEngine
{
    private static bool _isInitialized;

    public static bool IsInitialized => _isInitialized;

    public static Font? DefaultFont { get; set; }

    public static void Initialize()
    {
        if (_isInitialized) return;

        if (SDL3_ttf.TTF_Init())
        {
            _isInitialized = true;
            Console.WriteLine("[OpenRei FontEngine] SDL3_ttf initialized successfully.");

            string[] candidatePaths = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoogleSans-Regular.ttf"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenRei", "GoogleSans-Regular.ttf"),
                "GoogleSans-Regular.ttf",
                "OpenRei/GoogleSans-Regular.ttf",
                "../OpenRei/GoogleSans-Regular.ttf",
                "../../OpenRei/GoogleSans-Regular.ttf",
                @"E:\ProjectTemp-Server\OpenRei-cs\OpenRei\GoogleSans-Regular.ttf"
            };

            string? foundFontPath = candidatePaths.FirstOrDefault(File.Exists);

            if (foundFontPath != null)
            {
                DefaultFont = new Font(foundFontPath, 24.0f);
                Console.WriteLine($"[OpenRei FontEngine] Default font '{foundFontPath}' loaded successfully!");
            }
            else
            {
                Console.WriteLine("[OpenRei FontEngine Warning] Could not locate 'GoogleSans-Regular.ttf' in any search path.");
            }
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
