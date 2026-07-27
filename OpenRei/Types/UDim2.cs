namespace OpenRei.Types;

/// <summary>
/// Represents a 2D layout dimension containing X and Y UDims (Roblox-style UDim2).
/// Formula: AbsolutePosition = ParentPosition + (ParentSize * Scale) + Offset
/// </summary>
public readonly record struct UDim2(UDim X, UDim Y)
{
    public UDim X { get; init; } = X;
    public UDim Y { get; init; } = Y;

    public UDim2(float xScale, float yScale, float xOffset, float yOffset) 
        : this(new UDim(xScale, xOffset), new UDim(yScale, yOffset)) { }

    public static UDim2 Zero => new(UDim.Zero, UDim.Zero);
    public static UDim2 Full => new(UDim.Full, UDim.Full);

    public static UDim2 FromScale(float xScale, float yScale) => new(new UDim(xScale, 0f), new UDim(yScale, 0f));
    public static UDim2 FromOffset(float xOffset, float yOffset) => new(new UDim(0f, xOffset), new UDim(0f, yOffset));

    /// <summary>
    /// Computes absolute pixel vector relative to a parent size vector.
    /// </summary>
    public Vector2D GetAbsolute(Vector2D parentSize) => new(
        X.GetAbsolute(parentSize.X),
        Y.GetAbsolute(parentSize.Y)
    );

    public static UDim2 operator +(UDim2 a, UDim2 b) => new(a.X + b.X, a.Y + b.Y);
    public static UDim2 operator -(UDim2 a, UDim2 b) => new(a.X - b.X, a.Y - b.Y);
}
