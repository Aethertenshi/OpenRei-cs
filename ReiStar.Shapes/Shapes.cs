namespace reistar.Shapes;

using reistar.Maths;
using reistar.Graphics;

/// <summary>
/// Layer 2 Features Module: Immediate-mode style high-level primitive drawing abstractions.
/// </summary>
public static class Shapes
{
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

    public static void DrawRectOutline(
        IRenderer renderer,
        UVect position,
        UVect size,
        float thickness,
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

        renderer.DrawRectOutline(topLeftPos, resolvedSize, thickness, color, zIndex);
    }

    public static void DrawLine(
        IRenderer renderer,
        Vect2D start,
        Vect2D end,
        float thickness,
        Color color,
        int zIndex = 0)
    {
        renderer.DrawLine(start, end, thickness, color, zIndex);
    }

    public static void DrawCircle(
        IRenderer renderer,
        Vect2D center,
        float radius,
        Color color,
        int zIndex = 0)
    {
        renderer.DrawCircle(center, radius, color, zIndex);
    }

    public static void DrawTexture(
        IRenderer renderer,
        ITexture texture,
        UVect position,
        UVect size,
        Color tint = default,
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

        renderer.DrawTexture(texture, topLeftPos, resolvedSize, tint.A == 0 ? Color.White : tint, zIndex);
    }

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
