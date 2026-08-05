namespace reistar.Shapes;

using reistar.Maths;
using reistar.Graphics;

public static class Shapes
{
    /// <summary>
    /// Draws a 2D rectangle using responsive UVect position & size, Anchor alignment, and Z-Index sorting.
    /// </summary>
    public static void DrawRect(
        IRenderer renderer,
        UVect position,
        UVect size,
        Color color,
        Anchor anchor = default,
        int zIndex = 0)
    {
        Vect2D canvasSize = renderer.CanvasSize;
        Vect2D resolvedSize = size.Resolve(canvasSize);
        Vect2D rawPos = position.Resolve(canvasSize);

        // Apply anchor point offset
        Vect2D topLeftPos = new Vect2D(
            rawPos.X - (resolvedSize.X * anchor.X),
            rawPos.Y - (resolvedSize.Y * anchor.Y)
        );

        renderer.DrawRect(topLeftPos, resolvedSize, color, zIndex);
    }

    /// <summary>
    /// Draws a 2D rectangle using raw absolute pixel coordinates, Anchor alignment, and Z-Index sorting.
    /// </summary>
    public static void DrawRect(
        IRenderer renderer,
        Vect2D position,
        Vect2D size,
        Color color,
        Anchor anchor = default,
        int zIndex = 0)
    {
        Vect2D topLeftPos = new Vect2D(
            position.X - (size.X * anchor.X),
            position.Y - (size.Y * anchor.Y)
        );

        renderer.DrawRect(topLeftPos, size, color, zIndex);
    }
}
