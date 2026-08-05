namespace reistar.Shapes;

using reistar.Maths;
using reistar.Graphics;

public static class Shapes
{
    public static void DrawRect(IRenderer renderer, UVect position, UVect size, Color color, int zIndex = 0)
    {
        Vect2D canvasSize = renderer.CanvasSize;
        Vect2D resolvedSize = size.Resolve(canvasSize);
        Vect2D rawPos = position.Resolve(canvasSize);
        renderer.DrawRect(rawPos, resolvedSize, color, zIndex);
    }

    public static void DrawRect(IRenderer renderer, Vect2D position, Vect2D size, Color color, int zIndex = 0)
    {
        renderer.DrawRect(position, size, color, zIndex);
    }
}
