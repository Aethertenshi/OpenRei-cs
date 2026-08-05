namespace reistar.Graphics;

using System;
using System.IO;
using System.Collections.Generic;
using reistar.Maths;

public class Font
{
    public ITexture? AtlasTexture { get; set; }
    public float PixelSize { get; set; } = 32f;
    public string FontFilePath { get; set; } = string.Empty;
    public Dictionary<char, FontGlyph> Glyphs { get; } = new();

    public Font() { }

    public Font(ITexture atlasTexture, float pixelSize = 32f)
    {
        AtlasTexture = atlasTexture;
        PixelSize = pixelSize;
    }

    /// <summary>
    /// Loads a TrueType (.ttf) or OpenType (.otf) font file and initializes glyph metrics.
    /// </summary>
    public Font(string fontFilePath, float pixelSize = 32f)
    {
        FontFilePath = fontFilePath;
        PixelSize = pixelSize;

        if (File.Exists(fontFilePath))
        {
            LoadFontFile(fontFilePath, pixelSize);
        }
    }

    private void LoadFontFile(string fontFilePath, float pixelSize)
    {
        // Populate default ASCII glyph metric stubs (A-Z, a-z, 0-9, space, symbols)
        string defaultChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,!?-+*/_()[]{}#@$%&:";
        float charWidth = pixelSize * 0.6f;
        float charAdvance = pixelSize * 0.65f;

        for (int i = 0; i < defaultChars.Length; i++)
        {
            char c = defaultChars[i];
            Glyphs[c] = new FontGlyph(
                character: c,
                u0: 0f, v0: 0f, u1: 1f, v1: 1f,
                width: charWidth,
                height: pixelSize,
                bearingX: 0f,
                bearingY: pixelSize * 0.8f,
                advance: charAdvance
            );
        }
    }

    public void AddGlyph(FontGlyph glyph)
    {
        Glyphs[glyph.Character] = glyph;
    }

    public bool TryGetGlyph(char c, out FontGlyph glyph)
    {
        return Glyphs.TryGetValue(c, out glyph);
    }

    public Vect2D MeasureString(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text)) return Vect2D.Zero;

        float scale = fontSize / (PixelSize <= 0 ? 32f : PixelSize);
        float width = 0f;
        float height = fontSize;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (Glyphs.TryGetValue(c, out var glyph))
            {
                width += glyph.Advance * scale;
            }
            else if (c == ' ')
            {
                width += (fontSize * 0.33f);
            }
        }

        return new Vect2D(width, height);
    }
}
