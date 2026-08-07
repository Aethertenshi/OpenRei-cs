namespace reistar.Graphics;

using System;
using System.IO;
using System.Collections.Generic;
using SDL;
using reistar.Maths;

/// <summary>
/// Represents a loaded TrueType/OpenType font family/file with per-size handle caching,
/// size-quantizing protection, LRU eviction, dynamic GPU glyph atlas caching, and text measurement using SDL3_ttf.
/// </summary>
public unsafe class Font : IDisposable
{
    private const int MaxCachedSizes = 64;
    private readonly string? _filePath;
    private readonly Dictionary<int, IntPtr> _sizeHandles = new();
    private readonly Queue<int> _evictionQueue = new();

    // Atlas Packing State
    private readonly Dictionary<int, FontGlyph> _glyphCache = new();
    private int _atlasWidth = 1024;
    private int _atlasHeight = 1024;
    private byte[]? _atlasPixels;
    private int _currentX = 2;
    private int _currentY = 2;
    private int _rowHeight = 0;
    private bool _isDisposed;

    public string? FilePath => _filePath;
    public float DefaultSize { get; set; } = 32.0f;
    public float PixelSize => DefaultSize;
    public ITexture? AtlasTexture { get; private set; }

    public Font() { }

    public Font(string path, float defaultSize = 32.0f)
    {
        _filePath = path;
        DefaultSize = defaultSize;
    }

    /// <summary>
    /// Gets or creates the native SDL3 TTF_Font handle for the requested point size and outline thickness.
    /// Quantized to 0.5pt steps with LRU size limit (max 64 entries) for memory leak protection.
    /// </summary>
    public TTF_Font* GetHandle(float fontSize, int outline = 0)
    {
        if (_isDisposed || string.IsNullOrEmpty(_filePath)) return null;

        int sizeKey = (int)MathF.Round(fontSize * 2f);
        int key = (sizeKey << 8) | (outline & 0xFF);

        if (_sizeHandles.TryGetValue(key, out var existingHandle))
        {
            return (TTF_Font*)existingHandle;
        }

        if (!File.Exists(_filePath))
        {
            return null;
        }

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
            {
                SDL3_ttf.TTF_SetFontOutline(handle, outline);
            }
            _sizeHandles[key] = (IntPtr)handle;
            _evictionQueue.Enqueue(key);
        }

        return handle;
    }

    /// <summary>
    /// Retrieves a cached glyph from the persistent GPU font atlas or rasterizes and packs it on demand.
    /// </summary>
    public bool TryGetGlyph(char c, float fontSize, IRenderer renderer, out FontGlyph glyph)
    {
        int sizeKey = (int)MathF.Round(fontSize * 2f);
        int cacheKey = (c << 16) | (sizeKey & 0xFFFF);

        if (_glyphCache.TryGetValue(cacheKey, out glyph))
        {
            return true;
        }

        TTF_Font* fontHandle = GetHandle(fontSize);
        if (fontHandle == null)
        {
            glyph = default;
            return false;
        }

        int minx = 0, maxx = 0, miny = 0, maxy = 0, advance = 0;
        SDL3_ttf.TTF_GetGlyphMetrics(fontHandle, (uint)c, &minx, &maxx, &miny, &maxy, &advance);

        SDL_Color white = new SDL_Color { r = 255, g = 255, b = 255, a = 255 };
        SDL_Surface* glyphSurf = SDL3_ttf.TTF_RenderGlyph_Blended(fontHandle, (uint)c, white);

        if (glyphSurf == null || glyphSurf->w <= 0 || glyphSurf->h <= 0)
        {
            if (glyphSurf != null) SDL3.SDL_DestroySurface(glyphSurf);

            glyph = new FontGlyph(
                character: c,
                u0: 0f, v0: 0f, u1: 0f, v1: 0f,
                width: 0f, height: 0f,
                bearingX: 0f, bearingY: 0f,
                advance: advance > 0 ? advance : fontSize * 0.33f
            );
            _glyphCache[cacheKey] = glyph;
            return true;
        }

        int gw = glyphSurf->w;
        int gh = glyphSurf->h;

        // Initialize atlas CPU pixel buffer if needed
        if (_atlasPixels == null)
        {
            _atlasPixels = new byte[_atlasWidth * _atlasHeight * 4];
        }

        // Shelf packing logic for font atlas
        if (_currentX + gw + 2 >= _atlasWidth)
        {
            _currentX = 2;
            _currentY += _rowHeight + 2;
            _rowHeight = 0;
        }

        if (_currentY + gh + 2 >= _atlasHeight)
        {
            // Atlas full: fallback for oversized glyphs
            SDL3.SDL_DestroySurface(glyphSurf);
            glyph = default;
            return false;
        }

        // Copy glyph surface pixels into atlas pixel array
        byte* srcPixels = (byte*)glyphSurf->pixels;
        int pitch = glyphSurf->pitch;

        for (int y = 0; y < gh; y++)
        {
            byte* srcRow = srcPixels + (y * pitch);
            int dstPixelOffset = (((_currentY + y) * _atlasWidth) + _currentX) * 4;

            for (int x = 0; x < gw; x++)
            {
                int srcIdx = x * 4;
                int dstIdx = dstPixelOffset + (x * 4);

                _atlasPixels[dstIdx + 0] = srcRow[srcIdx + 0]; // R
                _atlasPixels[dstIdx + 1] = srcRow[srcIdx + 1]; // G
                _atlasPixels[dstIdx + 2] = srcRow[srcIdx + 2]; // B
                _atlasPixels[dstIdx + 3] = srcRow[srcIdx + 3]; // A
            }
        }

        float u0 = _currentX / (float)_atlasWidth;
        float v0 = _currentY / (float)_atlasHeight;
        float u1 = (_currentX + gw) / (float)_atlasWidth;
        float v1 = (_currentY + gh) / (float)_atlasHeight;

        _currentX += gw + 2;
        _rowHeight = Math.Max(_rowHeight, gh);

        SDL3.SDL_DestroySurface(glyphSurf);

        // Update GPU Atlas Texture
        if (AtlasTexture == null)
        {
            AtlasTexture = renderer.CreateTexture(_atlasWidth, _atlasHeight, _atlasPixels);
        }
        else
        {
            AtlasTexture.UpdateTexture(_atlasPixels);
        }

        glyph = new FontGlyph(
            character: c,
            u0: u0, v0: v0, u1: u1, v1: v1,
            width: gw, height: gh,
            bearingX: minx, bearingY: maxy,
            advance: advance
        );

        _glyphCache[cacheKey] = glyph;
        return true;
    }

    /// <summary>
    /// Measures the pixel dimensions of a string rendered at a specific point size.
    /// </summary>
    public Vect2D MeasureString(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text)) return Vect2D.Zero;

        TTF_Font* handle = GetHandle(fontSize);
        if (handle == null) return Vect2D.Zero;

        int width = 0, height = 0;
        if (SDL3_ttf.TTF_GetStringSize(handle, text, (nuint)text.Length, &width, &height))
        {
            return new Vect2D(width, height);
        }

        return Vect2D.Zero;
    }

    public Vect2D MeasureString(string text) => MeasureString(text, DefaultSize);

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
            _glyphCache.Clear();

            AtlasTexture?.Dispose();
            AtlasTexture = null;

            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
