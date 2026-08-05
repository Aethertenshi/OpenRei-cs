namespace reistar.Graphics;

using System;
using System.Collections.Generic;
using reistar.Maths;

public class Font
{
    public ITexture? AtlasTexture { get; set; }
    public float PixelSize { get; set; } = 32f;
    public Dictionary<char, FontGlyph> Glyphs { get; } = new();

    public Font() { }

    public Font(ITexture atlasTexture, float pixelSize)
    {
        AtlasTexture = atlasTexture;
        PixelSize = pixelSize;
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
