namespace OpenRei.Types;

/// <summary>
/// Represents a 1D scalar layout dimension combining a relative scale and an absolute offset.
/// </summary>
public readonly record struct UDim(float Scale, float Offset)
{
    public static UDim Zero => new(0f, 0f);
    public static UDim Full => new(1.0f, 0f);

    public static UDim FromScale(float scale) => new(scale, 0f);
    public static UDim FromOffset(float offset) => new(0f, offset);

    /// <summary>
    /// Computes the absolute pixel value relative to a parent length.
    /// </summary>
    public float GetAbsolute(float parentLength) => (parentLength * Scale) + Offset;

    public static UDim operator +(UDim a, UDim b) => new(a.Scale + b.Scale, a.Offset + b.Offset);
    public static UDim operator -(UDim a, UDim b) => new(a.Scale - b.Scale, a.Offset - b.Offset);
}
