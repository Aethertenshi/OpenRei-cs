namespace reistar.Maths;

public struct Vect2D
{
    public float X;
    public float Y;

    public Vect2D(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static readonly Vect2D Zero = new(0f, 0f);
    public static readonly Vect2D One = new(1f, 1f);

    public static Vect2D operator +(Vect2D a, Vect2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Vect2D operator -(Vect2D a, Vect2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Vect2D operator *(Vect2D a, float scalar) => new(a.X * scalar, a.Y * scalar);
    public static Vect2D operator /(Vect2D a, float scalar) => new(a.X / scalar, a.Y / scalar);

    public override string ToString() => $"({X}, {Y})";
}
