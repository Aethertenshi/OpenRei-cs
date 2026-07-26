namespace OpenRei.Types;

/// <summary>
/// A 2D floating-point vector used for positions, sizes, and scale calculations.
/// </summary>
public readonly struct Vector2D : IEquatable<Vector2D>
{
    public float X { get; }
    public float Y { get; }

    public Vector2D(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vector2D Zero => new(0f, 0f);
    public static Vector2D One => new(1f, 1f);

    public float LengthSquared => (X * X) + (Y * Y);
    public float Length => MathF.Sqrt(LengthSquared);

    public Vector2D GetNormalized()
    {
        float len = Length;
        return len > 0f ? this / len : Zero;
    }

    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2D operator *(Vector2D v, float scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vector2D operator *(float scalar, Vector2D v) => new(v.X * scalar, v.Y * scalar);
    public static Vector2D operator *(Vector2D a, Vector2D b) => new(a.X * b.X, a.Y * b.Y);
    public static Vector2D operator /(Vector2D v, float scalar) => new(v.X / scalar, v.Y / scalar);

    public bool Equals(Vector2D other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Vector2D other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"Vector2D({X}, {Y})";
}
