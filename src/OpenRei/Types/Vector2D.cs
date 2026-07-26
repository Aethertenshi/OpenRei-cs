namespace OpenRei.Types;

/// <summary>
/// A 2D floating-point vector used for positions, sizes, and scale calculations.
/// </summary>
public readonly record struct Vector2D(float X, float Y)
{
    public static Vector2D Zero => new(0f, 0f);
    public static Vector2D One => new(1f, 1f);

    public float LengthSquared => (X * X) + (Y * Y);
    public float Length => MathF.Sqrt(LengthSquared);

    public Vector2D Normalized => Length > 0f ? this / Length : Zero;

    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2D operator *(Vector2D v, float scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vector2D operator *(float scalar, Vector2D v) => new(v.X * scalar, v.Y * scalar);
    public static Vector2D operator *(Vector2D a, Vector2D b) => new(a.X * b.X, a.Y * b.Y);
    public static Vector2D operator /(Vector2D v, float scalar) => new(v.X / scalar, v.Y / scalar);
}
