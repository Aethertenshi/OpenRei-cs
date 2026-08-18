namespace reistar.Graphics;

using System;
using System.IO;
using System.Collections.Generic;
using SDL;
using reistar.Maths;

/// <summary>
/// Represents a TrueType/OpenType font with an integrated GPU Glyph Texture Atlas,
/// baseline alignment metrics, sub-pixel advance calculation, and single-draw-call geometry batching.
/// </summary>
public unsafe class Font : IDisposable
{
    private const int AtlasWidth = 1024;
    private const int AtlasHeight = 1024;

    private readonly string? _filePath;
    private readonly Dictionary<char, FontGlyph> _glyphs = new();
    private TTF_Font* _fontHandle;
    private ITexture? _atlasTexture;
    private bool _isDisposed;

    public string? FilePath => _filePath;
    public float DefaultSize { get; set; } = 32.0f;
    public float PixelSize => DefaultSize;

    public float Ascent { get; private set; }
    public float Descent { get; private set; }
    public float LineHeight { get; private set; }
    public ITexture? AtlasTexture => _atlasTexture;

    public Font() { }

    public Font(string path, float defaultSize = 32.0f)
    {
        _filePath = path;
        DefaultSize = defaultSize;
    }

    /// <summary>
    /// Gets the native SDL3 TTF_Font handle for the font at the default point size.
    /// </summary>
    public TTF_Font* GetHandle(float fontSize = 0f, int outline = 0)
    {
        if (_isDisposed || string.IsNullOrEmpty(_filePath)) return null;

        if (_fontHandle == null)
        {
            if (!File.Exists(_filePath)) return null;
            _fontHandle = SDL3_ttf.TTF_OpenFont(_filePath, DefaultSize);
            if (_fontHandle != null)
            {
                Ascent = SDL3_ttf.TTF_GetFontAscent(_fontHandle);
                Descent = SDL3_ttf.TTF_GetFontDescent(_fontHandle);
                LineHeight = SDL3_ttf.TTF_GetFontLineSkip(_fontHandle);

                if (Ascent <= 0) Ascent = DefaultSize * 0.8f;
                if (LineHeight <= 0) LineHeight = (Ascent - Descent) > 0 ? (Ascent - Descent) : DefaultSize;
            }
        }

        return _fontHandle;
    }

    /// <summary>
    /// Retrieves a cached glyph for the character.
    /// </summary>
    public bool TryGetGlyph(char c, out FontGlyph glyph)
    {
        return _glyphs.TryGetValue(c, out glyph);
    }

    /// <summary>
    /// Ensures the glyph atlas texture has been generated and uploaded to the GPU renderer.
    /// </summary>
    public ITexture? GetOrGenerateAtlas(IRenderer renderer)
    {
        if (_atlasTexture != null) return _atlasTexture;
        if (_isDisposed || string.IsNullOrEmpty(_filePath)) return null;

        TTF_Font* handle = GetHandle();
        if (handle == null) return null;

        SDL_Surface* atlasSurf = SDL3.SDL_CreateSurface(AtlasWidth, AtlasHeight, SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888);
        if (atlasSurf == null) return null;

        // Clear master atlas to fully transparent
        SDL3.SDL_FillSurfaceRect(atlasSurf, null, 0);

        int shelfX = 1;
        int shelfY = 1;
        int shelfRowHeight = 0;
        SDL_Color white = new SDL_Color { r = 255, g = 255, b = 255, a = 255 };

        // Pre-bake ASCII printable characters from 32 (space) to 126 (~)
        for (char c = (char)32; c <= (char)126; c++)
        {
            int minx = 0, maxx = 0, miny = 0, maxy = 0, advance = 0;
            SDL3_ttf.TTF_GetGlyphMetrics(handle, (uint)c, &minx, &maxx, &miny, &maxy, &advance);

            if (c == ' ')
            {
                int spaceAdv = advance > 0 ? advance : (int)(DefaultSize * 0.28f);
                _glyphs[' '] = new FontGlyph(' ', 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, spaceAdv);
                continue;
            }

            SDL_Surface* glyphSurf = SDL3_ttf.TTF_RenderGlyph_Blended(handle, (uint)c, white);
            if (glyphSurf != null)
            {
                SDL_Surface* rgbaSurf = SDL3.SDL_ConvertSurface(glyphSurf, SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888);
                SDL3.SDL_DestroySurface(glyphSurf);

                if (rgbaSurf != null)
                {
                    int gw = rgbaSurf->w;
                    int gh = rgbaSurf->h;

                    // Shelf row wrapping
                    if (shelfX + gw + 1 >= AtlasWidth)
                    {
                        shelfX = 1;
                        shelfY += shelfRowHeight + 1;
                        shelfRowHeight = 0;
                    }

                    if (shelfY + gh + 1 < AtlasHeight)
                    {
                        SDL_Rect dstRect = new SDL_Rect { x = shelfX, y = shelfY, w = gw, h = gh };
                        // CRITICAL: Disable blend mode so glyph alpha channels are copied directly into destination atlas
                        SDL3.SDL_SetSurfaceBlendMode(rgbaSurf, SDL_BlendMode.SDL_BLENDMODE_NONE);
                        SDL3.SDL_BlitSurface(rgbaSurf, null, atlasSurf, &dstRect);

                        float u0 = (float)shelfX / AtlasWidth;
                        float v0 = (float)shelfY / AtlasHeight;
                        float u1 = (float)(shelfX + gw) / AtlasWidth;
                        float v1 = (float)(shelfY + gh) / AtlasHeight;

                        _glyphs[c] = new FontGlyph(
                            character: c,
                            u0: u0, v0: v0, u1: u1, v1: v1,
                            width: gw,
                            height: gh,
                            bearingX: minx,
                            bearingY: maxy,
                            advance: advance > 0 ? advance : (gw + 1)
                        );

                        shelfX += gw + 1;
                        shelfRowHeight = Math.Max(shelfRowHeight, gh);
                    }

                    SDL3.SDL_DestroySurface(rgbaSurf);
                }
            }
            else
            {
                _glyphs[c] = new FontGlyph(c, 0f, 0f, 0f, 0f, 0f, 0f, minx, maxy, advance > 0 ? advance : (DefaultSize * 0.5f));
            }
        }

        // Upload atlas surface pixels to GPU
        byte[] pixels = new byte[AtlasWidth * AtlasHeight * 4];
        fixed (byte* pDst = pixels)
        {
            Buffer.MemoryCopy((void*)atlasSurf->pixels, pDst, pixels.Length, pixels.Length);
        }
        SDL3.SDL_DestroySurface(atlasSurf);

        _atlasTexture = renderer.CreateTexture(AtlasWidth, AtlasHeight, pixels);
        return _atlasTexture;
    }

    /// <summary>
    /// Backwards compatible string texture provider (falls back to atlas).
    /// </summary>
    public ITexture? GetRenderedStringTexture(IRenderer renderer, string text, float fontSize, Color color)
    {
        return GetOrGenerateAtlas(renderer);
    }

    /// <summary>
    /// Measures the pixel dimensions of a string rendered at a specific point size with zero allocations.
    /// </summary>
    public Vect2D MeasureString(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text)) return Vect2D.Zero;

        // Ensure metrics are loaded
        if (_fontHandle == null) GetHandle();

        float scale = (DefaultSize > 0f) ? (fontSize / DefaultSize) : 1.0f;
        float currentLineWidth = 0f;
        float maxLineWidth = 0f;
        float scaledLineHeight = (LineHeight > 0f ? LineHeight : (Ascent - Descent)) * scale;
        if (scaledLineHeight <= 0f) scaledLineHeight = fontSize;
        int lineCount = 1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r') continue;
            if (c == '\n')
            {
                if (currentLineWidth > maxLineWidth) maxLineWidth = currentLineWidth;
                currentLineWidth = 0f;
                lineCount++;
                continue;
            }

            if (TryGetGlyph(c, out var glyph))
            {
                currentLineWidth += glyph.Advance * scale;
            }
            else
            {
                currentLineWidth += (fontSize * 0.28f);
            }
        }

        if (currentLineWidth > maxLineWidth) maxLineWidth = currentLineWidth;
        return new Vect2D(maxLineWidth, scaledLineHeight * lineCount);
    }

    public Vect2D MeasureString(string text) => MeasureString(text, DefaultSize);

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_fontHandle != null)
            {
                try
                {
                    SDL3_ttf.TTF_CloseFont(_fontHandle);
                }
                catch
                {
                    // Ignore if native TTF subsystem was already uninitialized
                }
                _fontHandle = null;
            }

            _atlasTexture?.Dispose();
            _atlasTexture = null;
            _glyphs.Clear();

            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
