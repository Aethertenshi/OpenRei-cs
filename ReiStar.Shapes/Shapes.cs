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

    /// <summary>
    /// Draws MSDF text using responsive UVect position, Anchor alignment, and Z-Index sorting.
    /// </summary>
    public static void DrawText(
        IRenderer renderer,
        Font font,
        string text,
        UVect position,
        float fontSize,
        Color color,
        Anchor anchor = default,
        int zIndex = 0)
    {
        Vect2D canvasSize = renderer.CanvasSize;
        Vect2D rawPos = position.Resolve(canvasSize);

        Vect2D textSize = font.MeasureString(text, fontSize);
        Vect2D topLeftPos = new Vect2D(
            rawPos.X - (textSize.X * anchor.X),
            rawPos.Y - (textSize.Y * anchor.Y)
        );

        renderer.DrawText(font, text, topLeftPos, fontSize, color, zIndex);
    }

    /// <summary>
    /// Draws MSDF text using absolute pixel coordinates, Anchor alignment, and Z-Index sorting.
    /// </summary>
    public static void DrawText(
        IRenderer renderer,
        Font font,
        string text,
        Vect2D position,
        float fontSize,
        Color color,
        Anchor anchor = default,
        int zIndex = 0)
    {
        Vect2D textSize = font.MeasureString(text, fontSize);
        Vect2D topLeftPos = new Vect2D(
            position.X - (textSize.X * anchor.X),
            position.Y - (textSize.Y * anchor.Y)
        );

        renderer.DrawText(font, text, topLeftPos, fontSize, color, zIndex);
    }
}
