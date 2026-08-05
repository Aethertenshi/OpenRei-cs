namespace reistar.Graphics;

public struct FontGlyph
{
    public char Character;
    public float U0;
    public float V0;
    public float U1;
    public float V1;
    public float Width;
    public float Height;
    public float BearingX;
    public float BearingY;
    public float Advance;

    public FontGlyph(
        char character,
        float u0, float v0, float u1, float v1,
        float width, float height,
        float bearingX, float bearingY,
        float advance)
    {
        Character = character;
        U0 = u0;
        V0 = v0;
        U1 = u1;
        V1 = v1;
        Width = width;
        Height = height;
        BearingX = bearingX;
        BearingY = bearingY;
        Advance = advance;
    }
}
