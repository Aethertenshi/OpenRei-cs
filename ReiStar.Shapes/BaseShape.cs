namespace reistar.Shapes;

using reistar.Maths;

public abstract class BaseShape
{
    public UVect Position { get; set; }
    public UVect Size { get; set; }
    public Anchor Anchor { get; set; } = Anchor.TopLeft;
    public Color Color { get; set; } = Color.White;
    public int ZIndex { get; set; } = 0;

    protected BaseShape() { }

    protected BaseShape(UVect position, UVect size, Anchor anchor, Color color, int zIndex = 0)
    {
        Position = position;
        Size = size;
        Anchor = anchor;
        Color = color;
        ZIndex = zIndex;
    }

    /// <summary>
    /// Resolves the absolute screen position and size given a parent canvas dimensions.
    /// Takes into account scale/offset calculation and anchor offset.
    /// </summary>
    public (Vect2D TopLeftPosition, Vect2D ResolvedSize) GetResolvedBounds(Vect2D parentCanvasSize)
    {
        Vect2D resolvedSize = Size.Resolve(parentCanvasSize);
        Vect2D anchorPosition = Position.Resolve(parentCanvasSize);

        // Subtract anchor point offset
        Vect2D topLeftPos = new Vect2D(
            anchorPosition.X - (resolvedSize.X * Anchor.X),
            anchorPosition.Y - (resolvedSize.Y * Anchor.Y)
        );

        return (topLeftPos, resolvedSize);
    }
}
