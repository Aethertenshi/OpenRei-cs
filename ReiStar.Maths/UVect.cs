namespace reistar.Maths;

public struct UVect
{
    public float ScaleX;
    public float OffsetX;
    public float ScaleY;
    public float OffsetY;

    public UVect(float scaleX, float offsetX, float scaleY, float offsetY)
    {
        ScaleX = scaleX;
        OffsetX = offsetX;
        ScaleY = scaleY;
        OffsetY = offsetY;
    }

    public static UVect FromOffset(float offsetX, float offsetY) => new(0f, offsetX, 0f, offsetY);
    public static UVect FromScale(float scaleX, float scaleY) => new(scaleX, 0f, scaleY, 0f);

    /// <summary>
    /// Calculates resolved pixel coordinates: (parentSize * Scale) + Offset
    /// </summary>
    public Vect2D Resolve(Vect2D parentSize)
    {
        return new Vect2D(
            (parentSize.X * ScaleX) + OffsetX,
            (parentSize.Y * ScaleY) + OffsetY
        );
    }

    public Vect2D Resolve(float parentWidth, float parentHeight)
    {
        return new Vect2D(
            (parentWidth * ScaleX) + OffsetX,
            (parentHeight * ScaleY) + OffsetY
        );
    }

    public override string ToString() => $"UVect(X: {ScaleX}s + {OffsetX}px, Y: {ScaleY}s + {OffsetY}px)";
}
