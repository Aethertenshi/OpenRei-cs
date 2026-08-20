namespace reistar.Shapes;

using System;
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

    public static void DrawRectOutline(
        IRenderer renderer,
        Vect2D position,
        Vect2D size,
        float thickness,
        Color color,
        Anchor anchor = default,
        int zIndex = 0)
    {
        Vect2D topLeftPos = new Vect2D(
            position.X - (size.X * anchor.X),
            position.Y - (size.Y * anchor.Y)
        );

        renderer.DrawRectOutline(topLeftPos, size, thickness, color, zIndex);
    }

    public static void DrawRectRotated(
        IRenderer renderer,
        Vect2D position,
        Vect2D size,
        float angleDegrees,
        Vect2D pivot = default,
        Color color = default,
        Anchor anchor = default,
        int zIndex = 0)
    {
        Vect2D topLeftPos = new Vect2D(
            position.X - (size.X * anchor.X),
            position.Y - (size.Y * anchor.Y)
        );

        renderer.DrawRectRotated(topLeftPos, size, angleDegrees, pivot, color, zIndex);
    }

    public static void DrawTextureRotated(
        IRenderer renderer,
        ITexture texture,
        Vect2D position,
        Vect2D size,
        float angleDegrees,
        Vect2D pivot = default,
        Color tint = default,
        Anchor anchor = default,
        int zIndex = 0)
    {
        Vect2D topLeftPos = new Vect2D(
            position.X - (size.X * anchor.X),
            position.Y - (size.Y * anchor.Y)
        );

        renderer.DrawTextureRotated(texture, topLeftPos, size, angleDegrees, pivot, tint.A == 0 ? Color.White : tint, zIndex);
    }

    public static void DrawGeometry(
        IRenderer renderer,
        ITexture? texture,
        ReadOnlySpan<Vertex2D> vertices,
        ReadOnlySpan<int> indices,
        int zIndex = 0)
    {
        renderer.DrawGeometry(texture, vertices, indices, zIndex);
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

    public static void DrawThickLine(
        IRenderer renderer,
        Vect2D start,
        Vect2D end,
        float thickness,
        Color color,
        int zIndex = 0)
    {
        renderer.DrawLine(start, end, thickness, color, zIndex);
    }

    public static void DrawArc(
        IRenderer renderer,
        Vect2D center,
        float radius,
        float startAngle,
        float endAngle,
        float thickness,
        Color color,
        int segments = 16,
        int zIndex = 0)
    {
        float prevX = center.X + radius * MathF.Cos(startAngle);
        float prevY = center.Y + radius * MathF.Sin(startAngle);

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = startAngle + (endAngle - startAngle) * t;
            float nextX = center.X + radius * MathF.Cos(angle);
            float nextY = center.Y + radius * MathF.Sin(angle);

            renderer.DrawLine(new Vect2D(prevX, prevY), new Vect2D(nextX, nextY), thickness, color, zIndex);

            prevX = nextX;
            prevY = nextY;
        }
    }

    public static void DrawPolygon(
        IRenderer renderer,
        ReadOnlySpan<Vect2D> points,
        Color color,
        int zIndex = 0)
    {
        if (points.Length < 3) return;

        // Fan triangulation for convex polygons
        Span<Vertex2D> vertices = stackalloc Vertex2D[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            vertices[i] = new Vertex2D(points[i], color, Vect2D.Zero);
        }

        int indexCount = (points.Length - 2) * 3;
        Span<int> indices = stackalloc int[indexCount];
        int idx = 0;
        for (int i = 1; i < points.Length - 1; i++)
        {
            indices[idx++] = 0;
            indices[idx++] = i;
            indices[idx++] = i + 1;
        }

        renderer.DrawGeometry(null, vertices, indices, zIndex);
    }

    public static void DrawBackdropBlur(
        IRenderer renderer,
        Vect2D position,
        Vect2D size,
        float blurAmount = 1.0f,
        Anchor anchor = default,
        int zIndex = 0)
    {
        Vect2D topLeftPos = new Vect2D(
            position.X - (size.X * anchor.X),
            position.Y - (size.Y * anchor.Y)
        );

        renderer.DrawBackdropBlur(topLeftPos, size, blurAmount, zIndex);
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
