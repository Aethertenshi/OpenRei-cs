using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Represents a loaded TrueType/OpenType font file with size & text measurement capabilities.
/// </summary>
public unsafe class Font : IDisposable
{
    private TTF_Font* _fontHandle;
    private bool _isDisposed;

    public float Size { get; }
    public TTF_Font* Handle => _fontHandle;

    public Font(string path, float pointSize)
    {
        Size = pointSize;
        FontEngine.Initialize();

        if (FontEngine.IsInitialized)
        {
            _fontHandle = SDL3_ttf.TTF_OpenFont(path, pointSize);
            if (_fontHandle == null)
            {
                Console.WriteLine($"[Font Warning] Failed to load font '{path}': {SDL3.SDL_GetError()}");
            }
        }
    }

    /// <summary>
    /// Measures the pixel width and height of a string rendered with this font.
    /// </summary>
    public Vector2D MeasureString(string text)
    {
        if (_fontHandle == null || string.IsNullOrEmpty(text)) return Vector2D.Zero;

        int width = 0, height = 0;
        if (SDL3_ttf.TTF_GetStringSize(_fontHandle, text, (nuint)text.Length, &width, &height))
        {
            return new Vector2D(width, height);
        }

        return Vector2D.Zero;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_fontHandle != null)
            {
                SDL3_ttf.TTF_CloseFont(_fontHandle);
                _fontHandle = null;
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
