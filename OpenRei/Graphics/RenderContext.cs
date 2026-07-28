using OpenRei.Filters;
using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

public readonly struct TextCommand
{
    public readonly Font? Font;
    public readonly string Text;
    public readonly Rect Bounds;
    public readonly Color Color;
    public readonly float ZIndex;

    public TextCommand(Font? font, string text, Rect bounds, Color color, float zIndex = 1f)
    {
        Font = font;
        Text = text;
        Bounds = bounds;
        Color = color;
        ZIndex = zIndex;
    }
}

public readonly struct ImageCommand
{
    public readonly Texture Texture;
    public readonly Rect DestBounds;
    public readonly Rect? SourceRect;
    public readonly Color Color;
    public readonly BlurFilter? BlurFilter;
    public readonly float ZIndex;

    public ImageCommand(Texture texture, Rect destBounds, Rect? sourceRect, Color color, BlurFilter? blurFilter = null, float zIndex = 1f)
    {
        Texture = texture;
        DestBounds = destBounds;
        SourceRect = sourceRect;
        Color = color;
        BlurFilter = blurFilter;
        ZIndex = zIndex;
    }
}

/// <summary>
/// Execution context passed down scene graph elements to submit 2D quad instances, text, images, and texture blur filters.
/// </summary>
public class RenderContext
{
    private readonly RenderQueue _queue;

    internal unsafe SDL_Renderer* RendererHandle { get; set; }

    public List<TextCommand> TextCommands { get; } = new();
    public List<ImageCommand> ImageCommands { get; } = new();

    public RenderContext(RenderQueue queue)
    {
        _queue = queue;
    }

    public unsafe void PushClipRect(Rect bounds)
    {
        if (RendererHandle == null) return;
        SDL_Rect clipRect = new SDL_Rect
        {
            x = (int)bounds.X, y = (int)bounds.Y,
            w = (int)bounds.Width, h = (int)bounds.Height
        };
        SDL3.SDL_SetRenderClipRect(RendererHandle, &clipRect);
    }

    public unsafe void PopClipRect()
    {
        if (RendererHandle == null) return;
        SDL3.SDL_SetRenderClipRect(RendererHandle, null);
    }

    public void DrawQuad(Rect bounds, Color color, float cornerRadius = 0.0f, float zIndex = 1.0f)
    {
        _queue.Enqueue(new QuadInstance(bounds, color, cornerRadius, zIndex));
    }

    public void DrawText(Font? font, string text, Rect bounds, Color color, float zIndex = 1f)
    {
        if (!string.IsNullOrEmpty(text))
        {
            TextCommands.Add(new TextCommand(font, text, bounds, color, zIndex));
        }
    }

    public void DrawImage(Texture? texture, Rect destBounds, Rect? sourceRect = null, Color? color = null, BlurFilter? blurFilter = null, float zIndex = 1f)
    {
        if (texture != null && texture.IsValid)
        {
            ImageCommands.Add(new ImageCommand(texture, destBounds, sourceRect, color ?? Color.White, blurFilter, zIndex));
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
