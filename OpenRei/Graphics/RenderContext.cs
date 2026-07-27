using OpenRei.Types;

namespace OpenRei.Graphics;

public readonly struct TextCommand
{
    public readonly Font? Font;
    public readonly string Text;
    public readonly Rect Bounds;
    public readonly Color Color;

    public TextCommand(Font? font, string text, Rect bounds, Color color)
    {
        Font = font;
        Text = text;
        Bounds = bounds;
        Color = color;
    }
}

public readonly struct ImageCommand
{
    public readonly Texture Texture;
    public readonly Rect DestBounds;
    public readonly Rect? SourceRect;
    public readonly Color Color;

    public ImageCommand(Texture texture, Rect destBounds, Rect? sourceRect, Color color)
    {
        Texture = texture;
        DestBounds = destBounds;
        SourceRect = sourceRect;
        Color = color;
    }
}

/// <summary>
/// Execution context passed down scene graph elements to submit 2D quad instances, text, and images to the RenderQueue.
/// </summary>
public class RenderContext
{
    private readonly RenderQueue _queue;

    public List<TextCommand> TextCommands { get; } = new();
    public List<ImageCommand> ImageCommands { get; } = new();

    public RenderContext(RenderQueue queue)
    {
        _queue = queue;
    }

    public void DrawQuad(Rect bounds, Color color, float cornerRadius = 0.0f, float zIndex = 1.0f)
    {
        _queue.Enqueue(new QuadInstance(bounds, color, cornerRadius, zIndex));
    }

    public void DrawText(Font? font, string text, Rect bounds, Color color)
    {
        if (!string.IsNullOrEmpty(text))
        {
            TextCommands.Add(new TextCommand(font, text, bounds, color));
        }
    }

    public void DrawImage(Texture? texture, Rect destBounds, Rect? sourceRect = null, Color? color = null)
    {
        if (texture != null && texture.IsValid)
        {
            ImageCommands.Add(new ImageCommand(texture, destBounds, sourceRect, color ?? Color.White));
        }
    }

    public void DrawSpline(List<Vector2D> points, float strokeWidth, Color color, float zIndex = 1.0f)
    {
        if (points.Count < 2) return;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2D p0 = points[i];
            Vector2D p1 = points[i + 1];

            Vector2D diff = p1 - p0;
            float len = diff.Length;
            if (len <= 0.0001f) continue;

            Rect bounds = new Rect(p0.X, p0.Y - strokeWidth * 0.5f, len, strokeWidth);
            _queue.Enqueue(new QuadInstance(bounds, color, 0.0f, zIndex));
        }
    }
}
