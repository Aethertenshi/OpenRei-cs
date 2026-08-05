namespace reistar.Maths;

public struct Anchor
{
    public float X;
    public float Y;

    public Anchor(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static readonly Anchor TopLeft = new(0f, 0f);
    public static readonly Anchor TopCenter = new(0.5f, 0f);
    public static readonly Anchor TopRight = new(1f, 0f);
    public static readonly Anchor CenterLeft = new(0f, 0.5f);
    public static readonly Anchor Center = new(0.5f, 0.5f);
    public static readonly Anchor CenterRight = new(1f, 0.5f);
    public static readonly Anchor BottomLeft = new(0f, 1f);
    public static readonly Anchor BottomCenter = new(0.5f, 1f);
    public static readonly Anchor BottomRight = new(1f, 1f);

    public override string ToString() => $"Anchor({X}, {Y})";
}
