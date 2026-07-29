using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Represents a loaded TrueType/OpenType font family/file with per-size handle caching and text measurement.
/// </summary>
public unsafe class Font : IDisposable
{
    private readonly string? _filePath;
    private readonly Dictionary<int, IntPtr> _sizeHandles = new();
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
    /// Gets or creates the native SDL3 TTF_Font handle for the requested point size.
    /// Handles are cached per size for maximum rendering performance.
    /// </summary>
    public TTF_Font* GetHandle(float fontSize)
    {
        if (_isDisposed || string.IsNullOrEmpty(_filePath)) return null;

        int sizeKey = (int)MathF.Round(fontSize * 10f); // 0.1pt precision key
        if (_sizeHandles.TryGetValue(sizeKey, out var existingHandle))
        {
            return (TTF_Font*)existingHandle;
        }

        if (!File.Exists(_filePath))
        {
            Console.WriteLine($"[Font Warning] Font file not found at path '{_filePath}'");
            return null;
        }

        TTF_Font* handle = SDL3_ttf.TTF_OpenFont(_filePath, fontSize);
        if (handle != null)
        {
            _sizeHandles[sizeKey] = (IntPtr)handle;
        }
        else
        {
            Console.WriteLine($"[Font Warning] Failed to load font '{_filePath}' at {fontSize}pt: {SDL3.SDL_GetError()}");
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
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
