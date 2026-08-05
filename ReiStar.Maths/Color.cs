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

    public static readonly Color White = new(255, 255, 255, 255);
    public static readonly Color Black = new(0, 0, 0, 255);
    public static readonly Color Red = new(255, 0, 0, 255);
    public static readonly Color Green = new(0, 255, 0, 255);
    public static readonly Color Blue = new(0, 0, 255, 255);
    public static readonly Color Transparent = new(0, 0, 0, 0);

    public static Color FromHex(uint hex)
    {
        return new Color(
            (byte)((hex >> 24) & 0xFF),
            (byte)((hex >> 16) & 0xFF),
            (byte)((hex >> 8) & 0xFF),
            (byte)(hex & 0xFF)
        );
    }

    public override string ToString() => $"Color({R}, {G}, {B}, {A})";
}
