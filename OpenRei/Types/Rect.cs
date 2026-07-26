namespace OpenRei.Types;

/// <summary>
/// Defines an axis-aligned bounding box (AABB) in absolute screen space pixels.
/// </summary>
public readonly struct Rect : IEquatable<Rect>
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    public Rect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

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

    public bool Equals(Rect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is Rect other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"Rect(X={X}, Y={Y}, W={Width}, H={Height})";
}
