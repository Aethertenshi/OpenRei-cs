using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Controls SDL3_ttf font subsystem initialization, global font caching, and font lifecycle management.
/// </summary>
public static class FontEngine
{
    private static bool _isInitialized;
    private static readonly Dictionary<string, Font> _fontCache = new(StringComparer.OrdinalIgnoreCase);

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
                DefaultFont = LoadFont("Default", foundFontPath, 24.0f);
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

    /// <summary>
    /// Loads a TrueType/OpenType font from file path and caches it under the specified name.
    /// </summary>
    public static Font? LoadFont(string name, string path, float fontSize)
    {
        Initialize();

        if (_fontCache.TryGetValue(name, out var existingFont))
        {
            return existingFont;
        }

        if (!File.Exists(path))
        {
            Console.WriteLine($"[FontEngine Error] Font file not found at path '{path}'.");
            return null;
        }

        var font = new Font(path, fontSize);
        _fontCache[name] = font;
        Console.WriteLine($"[FontEngine] Font '{name}' ({fontSize}pt) loaded and cached successfully.");
        return font;
    }

    /// <summary>
    /// Retrieves a previously loaded font by name from cache.
    /// </summary>
    public static Font? GetFont(string name)
    {
        return _fontCache.TryGetValue(name, out var font) ? font : null;
    }

    /// <summary>
    /// Unloads and disposes a cached font by name.
    /// </summary>
    public static bool UnloadFont(string name)
    {
        if (_fontCache.Remove(name, out var font))
        {
            font.Dispose();
            Console.WriteLine($"[FontEngine] Font '{name}' unloaded and disposed successfully.");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Unloads and disposes all loaded fonts in cache.
    /// </summary>
    public static void UnloadAllFonts()
    {
        foreach (var font in _fontCache.Values)
        {
            font.Dispose();
        }
        _fontCache.Clear();
        Console.WriteLine("[FontEngine] All cached fonts unloaded.");
    }

    public static void Shutdown()
    {
        if (_isInitialized)
        {
            UnloadAllFonts();
            SDL3_ttf.TTF_Quit();
            _isInitialized = false;
        }
    }
}
