namespace OpenRei.Types;

/// <summary>
/// Represents an RGBA color supporting float (0..1), byte (0..255), int (0..255), and Hex string/uint constructors.
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

    public Color(byte r, byte g, byte b, byte a = 255)
        : this(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f) { }

    public Color(int r, int g, int b, int a = 255)
        : this(Math.Clamp(r, 0, 255) / 255.0f, Math.Clamp(g, 0, 255) / 255.0f, Math.Clamp(b, 0, 255) / 255.0f, Math.Clamp(a, 0, 255) / 255.0f) { }

    public static Color FromRgba(byte r, byte g, byte b, byte a = 255) =>
        new(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);

    /// <summary>
    /// Parses a hex color string like "#312925", "312925", "#312925FF", or "F00".
    /// </summary>
    public static Color FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return White;

        string clean = hex.TrimStart('#').Trim();

        // 3-digit shorthand #RGB -> #RRGGBB
        if (clean.Length == 3)
        {
            clean = $"{clean[0]}{clean[0]}{clean[1]}{clean[1]}{clean[2]}{clean[2]}";
        }
        // 4-digit shorthand #RGBA -> #RRGGBBAA
        else if (clean.Length == 4)
        {
            clean = $"{clean[0]}{clean[0]}{clean[1]}{clean[1]}{clean[2]}{clean[2]}{clean[3]}{clean[3]}";
        }

        if (clean.Length == 6)
        {
            if (byte.TryParse(clean.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                byte.TryParse(clean.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                byte.TryParse(clean.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                return FromRgba(r, g, b, 255);
            }
        }
        else if (clean.Length == 8)
        {
            if (byte.TryParse(clean.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                byte.TryParse(clean.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                byte.TryParse(clean.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b) &&
                byte.TryParse(clean.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out byte a))
            {
                return FromRgba(r, g, b, a);
            }
        }

        return White;
    }

    /// <summary>
    /// Parses an integer hex literal like 0x312925 (6-digit RGB) or 0x312925FF (8-digit RGBA).
    /// </summary>
    public static Color FromHex(uint hex)
    {
        if (hex <= 0xFFFFFF)
        {
            byte r = (byte)((hex >> 16) & 0xFF);
            byte g = (byte)((hex >> 8) & 0xFF);
            byte b = (byte)(hex & 0xFF);
            return FromRgba(r, g, b, 255);
        }
        else
        {
            byte r = (byte)((hex >> 24) & 0xFF);
            byte g = (byte)((hex >> 16) & 0xFF);
            byte b = (byte)((hex >> 8) & 0xFF);
            byte a = (byte)(hex & 0xFF);
            return FromRgba(r, g, b, a);
        }
    }

    public static Color FromHex(int hex) => FromHex((uint)hex);

    public Color WithAlpha(float alpha) => new(R, G, B, Math.Clamp(alpha, 0f, 1f));
}
