using OpenRei.Filters;
using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// A single FIFO command in the unified render queue.
/// </summary>
public readonly struct FifoCommand
{
    public readonly byte Type; // 0=Quad, 1=Image, 2=Text, 3=ClipPush, 4=ClipPop
    public readonly Rect Bounds;
    public readonly Color Color;
    public readonly float CornerRadius;
    public readonly float ZIndex;
    public readonly Texture? Texture;
    public readonly Rect? SourceRect;
    public readonly BlurFilter? BlurFilter;
    public readonly Font? Font;
    public readonly string? TextString;

    private FifoCommand(byte type, Rect bounds, Color color, float cornerRadius, float zIndex,
        Texture? texture = null, Rect? sourceRect = null, BlurFilter? blurFilter = null,
        Font? font = null, string? text = null)
    {
        Type = type; Bounds = bounds; Color = color; CornerRadius = cornerRadius; ZIndex = zIndex;
        Texture = texture; SourceRect = sourceRect; BlurFilter = blurFilter;
        Font = font; TextString = text;
    }

    internal static FifoCommand Quad(Rect b, Color c, float r, float z) => new(0, b, c, r, z);
    internal static FifoCommand Image(Texture t, Rect d, Rect? s, Color c, BlurFilter? b, float z) =>
        new(1, d, c, 0, z, texture: t, sourceRect: s, blurFilter: b);
    internal static FifoCommand MakeText(Font? f, string t, Rect b, Color c, float z) =>
        new(2, b, c, 0, z, font: f, text: t);
    internal static FifoCommand ClipPush(Rect b) => new(3, b, default, 0, 0);
    internal static FifoCommand ClipPop() => new(4, default, default, 0, 0);
}

/// <summary>
/// Execution context passed down scene graph elements to submit draw commands.
/// All commands are stored in a single FIFO list preserving traversal order.
/// </summary>
public class RenderContext
{
    private readonly List<FifoCommand> _commands = new();

    internal unsafe SDL_Renderer* RendererHandle { get; set; }

    public IReadOnlyList<FifoCommand> Commands => _commands;

    public RenderContext()
    {
    }

    public void PushClipRect(Rect bounds)
    {
        _commands.Add(FifoCommand.ClipPush(bounds));
    }

    public void PopClipRect()
    {
        _commands.Add(FifoCommand.ClipPop());
    }

    public void DrawQuad(Rect bounds, Color color, float cornerRadius = 0.0f, float zIndex = 1.0f)
    {
        _commands.Add(FifoCommand.Quad(bounds, color, cornerRadius, zIndex));
    }

    public void DrawText(Font? font, string text, Rect bounds, Color color, float zIndex = 1f)
    {
        if (!string.IsNullOrEmpty(text))
            _commands.Add(FifoCommand.MakeText(font, text, bounds, color, zIndex));
    }

    public void DrawImage(Texture? texture, Rect destBounds, Rect? sourceRect = null,
        Color? color = null, BlurFilter? blurFilter = null, float zIndex = 1f)
    {
        if (texture != null && texture.IsValid)
            _commands.Add(FifoCommand.Image(texture, destBounds, sourceRect,
                color ?? Color.White, blurFilter, zIndex));
    }

    public void DrawSpline(List<Vector2D> points, float strokeWidth, Color color, float zIndex = 1.0f)
    {
        if (points.Count < 2) return;
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2D p0 = points[i], p1 = points[i + 1];
            Vector2D diff = p1 - p0;
            float len = diff.Length;
            if (len <= 0.0001f) continue;
            _commands.Add(FifoCommand.Quad(
                new Rect(p0.X, p0.Y - strokeWidth * 0.5f, len, strokeWidth),
                color, 0.0f, zIndex));
        }
    }
}
