namespace OpenRei.Types;

/// <summary>
/// Defines per-corner rounding radii for Top-Left, Top-Right, Bottom-Left, and Bottom-Right corners.
/// </summary>
public readonly record struct CornerRadius(float TopLeft, float TopRight, float BottomLeft, float BottomRight)
{
    public static CornerRadius Zero => new(0f, 0f, 0f, 0f);

    public CornerRadius(float uniform) : this(uniform, uniform, uniform, uniform) { }

    public static CornerRadius FromCorners(float topLeft, float topRight, float bottomLeft, float bottomRight) =>
        new(topLeft, topRight, bottomLeft, bottomRight);

    public static implicit operator CornerRadius(float uniform) => new(uniform);
}
