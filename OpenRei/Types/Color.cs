namespace OpenRei.Types;

/// <summary>
/// Represents an RGBA color with both byte (0..255) and float (0..1) accessors.
/// </summary>
public readonly record struct Color(float R, float G, float B, float A = 1.0f)
{
    public static Color Transparent => new(0f, 0f, 0f, 0f);
    public static Color Black => new(0f, 0f, 0f, 1f);
    public static Color White => new(1f, 1f, 1f, 1f);
    public static Color Red => new(1f, 0f, 0f, 1f);
    public static Color Green => new(0f, 1f, 0f, 1f);
    public static Color Blue => new(0f, 0f, 1f, 1f);
    public static Color Yellow => new(1f, 1f, 0f, 1f);
    public static Color Cyan => new(0f, 1f, 1f, 1f);
    public static Color Magenta => new(1f, 0f, 1f, 1f);

    public static Color FromRgba(byte r, byte g, byte b, byte a = 255) => 
        new(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);

    public static Color FromHex(uint hex)
    {
        byte r = (byte)((hex >> 24) & 0xFF);
        byte g = (byte)((hex >> 16) & 0xFF);
        byte b = (byte)((hex >> 8) & 0xFF);
        byte a = (byte)(hex & 0xFF);
        return FromRgba(r, g, b, a);
    }

    public Color WithAlpha(float alpha) => new(R, G, B, Math.Clamp(alpha, 0f, 1f));
}
