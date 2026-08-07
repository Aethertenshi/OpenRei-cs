namespace reistar.Maths;

public struct Color
{
    public byte R;
    public byte G;
    public byte B;
    public byte A;

    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static Color Transparent => new(0, 0, 0, 0);
    public static Color White => new(255, 255, 255, 255);
    public static Color Black => new(0, 0, 0, 255);
    public static Color Red => new(255, 0, 0, 255);
    public static Color Green => new(0, 255, 0, 255);
    public static Color Blue => new(0, 0, 255, 255);
    public static Color Yellow => new(255, 255, 0, 255);
    public static Color Cyan => new(0, 255, 255, 255);
    public static Color Magenta => new(255, 0, 255, 255);
    public static Color Orange => new(255, 165, 0, 255);
    public static Color Purple => new(128, 0, 128, 255);
    public static Color Gray => new(128, 128, 128, 255);

    public override string ToString() => $"RGBA({R}, {G}, {B}, {A})";
}
