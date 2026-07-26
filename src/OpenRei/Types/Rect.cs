namespace OpenRei.Types;

/// <summary>
/// Defines an axis-aligned bounding box (AABB) in absolute screen space pixels.
/// </summary>
public readonly record struct Rect(float X, float Y, float Width, float Height)
{
    public Vector2D Position => new(X, Y);
    public Vector2D Size => new(Width, Height);

    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public bool Contains(Vector2D point) =>
        point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;

    public bool Intersects(Rect other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}
