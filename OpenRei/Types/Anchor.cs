namespace OpenRei.Types;

/// <summary>
/// Defines the normalized origin/pivot point (0.0 to 1.0) of an element for positioning math.
/// </summary>
public readonly record struct Anchor(float X, float Y)
{
    public static Anchor TopLeft => new(0f, 0f);
    public static Anchor TopCenter => new(0.5f, 0f);
    public static Anchor TopRight => new(1.0f, 0f);

    public static Anchor CenterLeft => new(0f, 0.5f);
    public static Anchor Center => new(0.5f, 0.5f);
    public static Anchor CenterRight => new(1.0f, 0.5f);

    public static Anchor BottomLeft => new(0f, 1.0f);
    public static Anchor BottomCenter => new(0.5f, 1.0f);
    public static Anchor BottomRight => new(1.0f, 1.0f);

    public Vector2D ToVector() => new(X, Y);
}
