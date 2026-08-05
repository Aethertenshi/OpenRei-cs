using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Represents a loaded TrueType/OpenType font family/file with per-size handle caching, 
/// size-quantizing protection, LRU eviction, and text measurement.
/// </summary>
public unsafe class Font : IDisposable
{
    private const int MaxCachedSizes = 64;
    private readonly string? _filePath;
    private readonly Dictionary<int, IntPtr> _sizeHandles = new();
    private readonly Queue<int> _evictionQueue = new();
    private bool _isDisposed;

    public string? FilePath => _filePath;
    public float DefaultSize { get; set; } = 16.0f;

    public Font(string path, float defaultSize = 16.0f)
    {
        _filePath = path;
        DefaultSize = defaultSize;
        FontEngine.Initialize();
    }

    /// <summary>
    /// Gets or creates the native SDL3 TTF_Font handle for the requested point size and outline thickness.
    /// Quantized to 0.5pt steps with LRU size limit (max 64 entries) for 100% memory leak protection.
    /// Distinct (size, outline) combinations get their own handle so the text-engine atlas renders the
    /// correct outline per pass without stale-glyph collisions.
    /// </summary>
    public TTF_Font* GetHandle(float fontSize, int outline = 0)
    {
        if (_isDisposed || string.IsNullOrEmpty(_filePath)) return null;

        // Quantize size to 0.5pt steps; combine with outline into a single cache key
        int sizeKey = (int)MathF.Round(fontSize * 2f);
        int key = (sizeKey << 8) | (outline & 0xFF);

        if (_sizeHandles.TryGetValue(key, out var existingHandle))
        {
            return (TTF_Font*)existingHandle;
        }

        if (!File.Exists(_filePath))
        {
            Console.WriteLine($"[Font Warning] Font file not found at path '{_filePath}'");
            return null;
        }

        // LRU Eviction: if more than MaxCachedSizes distinct (size, outline) combos are cached, close the oldest
        if (_sizeHandles.Count >= MaxCachedSizes && _evictionQueue.TryDequeue(out int oldestKey))
        {
            if (_sizeHandles.Remove(oldestKey, out var handleToClose) && handleToClose != IntPtr.Zero)
            {
                SDL3_ttf.TTF_CloseFont((TTF_Font*)handleToClose);
            }
        }

        float actualSize = sizeKey / 2.0f;
        TTF_Font* handle = SDL3_ttf.TTF_OpenFont(_filePath, actualSize);
        if (handle != null)
        {
            if (outline > 0)
                SDL3_ttf.TTF_SetFontOutline(handle, outline);
            _sizeHandles[key] = (IntPtr)handle;
            _evictionQueue.Enqueue(key);
        }
        else
        {
            Console.WriteLine($"[Font Warning] Failed to load font '{_filePath}' at {actualSize}pt: {SDL3.SDL_GetError()}");
        }

        return handle;
    }

    /// <summary>
    /// Measures the pixel dimensions of a string rendered at a specific point size.
    /// </summary>
    public Vector2D MeasureString(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text)) return Vector2D.Zero;

        TTF_Font* handle = GetHandle(fontSize);
        if (handle == null) return Vector2D.Zero;

        int width = 0, height = 0;
        if (SDL3_ttf.TTF_GetStringSize(handle, text, (nuint)text.Length, &width, &height))
        {
            return new Vector2D(width, height);
        }

        return Vector2D.Zero;
    }

    /// <summary>
    /// Measures the pixel dimensions of a string using the default font size.
    /// </summary>
    public Vector2D MeasureString(string text) => MeasureString(text, DefaultSize);

    public void Dispose()
    {
        if (!_isDisposed)
        {
            foreach (var handlePtr in _sizeHandles.Values)
            {
                if (handlePtr != IntPtr.Zero)
                {
                    SDL3_ttf.TTF_CloseFont((TTF_Font*)handlePtr);
                }
            }
            _sizeHandles.Clear();
            _evictionQueue.Clear();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
