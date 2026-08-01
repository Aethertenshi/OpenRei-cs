using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Controls SDL3_ttf font subsystem initialization, global font caching, and font lifecycle management.
/// </summary>
public static class FontEngine
{
    private static bool _isInitialized;
    private static readonly Dictionary<string, Font> _fontCache = new(StringComparer.OrdinalIgnoreCase);
    private static string? _defaultFontPath;

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
                "../../OpenRei/GoogleSans-Regular.ttf"
            };

            string? foundFontPath = candidatePaths.FirstOrDefault(File.Exists);

            if (foundFontPath != null)
            {
                _defaultFontPath = foundFontPath;
                DefaultFont = LoadFont("Default", foundFontPath, 16.0f);
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
    public static Font? LoadFont(string name, string path, float defaultSize = 16.0f)
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

        var font = new Font(path, defaultSize);
        _fontCache[name] = font;
        Console.WriteLine($"[FontEngine] Font '{name}' loaded and cached successfully.");
        return font;
    }

    /// <summary>
    /// Loads a TrueType/OpenType font from file path and caches it using the file path as key.
    /// </summary>
    public static Font? LoadFont(string path, float defaultSize = 16.0f) => LoadFont(path, path, defaultSize);

    /// <summary>
    /// Retrieves a previously loaded font by name or file path from cache.
    /// </summary>
    public static Font? GetFont(string name)
    {
        return _fontCache.TryGetValue(name, out var font) ? font : null;
    }

    /// <summary>
    /// Measures text at the given point size.
    /// If no font is provided, uses <see cref="DefaultFont"/>.
    /// </summary>
    public static Vector2D MeasureString(string text, float fontSize, Font? font = null)
    {
        font ??= DefaultFont;
        return font?.MeasureString(text, fontSize) ?? Vector2D.Zero;
    }

    /// <summary>
    /// Measures text using the provided font's default size or DefaultFont.
    /// </summary>
    public static Vector2D MeasureString(string text, Font? font = null)
    {
        font ??= DefaultFont;
        return font?.MeasureString(text) ?? Vector2D.Zero;
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
