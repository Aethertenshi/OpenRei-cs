namespace reistar.Shapes;

using reistar.Maths;
using reistar.Graphics;

public class Rectangle : BaseShape
{
    public Rectangle() { }

    public Rectangle(UVect position, UVect size, Color color, int zIndex = 0)
        : base(position, size, Anchor.TopLeft, color, zIndex) { }

    public Rectangle(UVect position, UVect size, Anchor anchor, Color color, int zIndex = 0)
        : base(position, size, anchor, color, zIndex) { }

    public void Draw(IRenderer renderer)
    {
        var (topLeft, resolvedSize) = GetResolvedBounds(renderer.CanvasSize);
        renderer.DrawRect(topLeft, resolvedSize, Color, ZIndex);
    }
}
