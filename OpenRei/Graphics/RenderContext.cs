using OpenRei.Filters;
using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// A single FIFO command in the unified render queue.
/// </summary>
public readonly struct FifoCommand
{
    public readonly byte Type; // 0=Quad, 1=Image, 2=Text, 3=ClipPush, 4=ClipPop, 5=BlurRegion, 6=Stroke, 7=Polygon
    public readonly Rect Bounds;
    public readonly Color Color;
    public readonly CornerRadius CornerRadius;
    public readonly float ZIndex;
    public readonly Texture? Texture;
    public readonly Rect? SourceRect;
    public readonly BlurFilter? BlurFilter;
    public readonly Font? Font;
    public readonly float FontSize;
    public readonly string? TextString;
    public readonly StrokeInfo Stroke;
    public readonly List<Vector2D>? Points;

    private FifoCommand(byte type, Rect bounds, Color color, CornerRadius cornerRadius, float zIndex,
        Texture? texture = null, Rect? sourceRect = null, BlurFilter? blurFilter = null,
        Font? font = null, float fontSize = 16.0f, string? text = null, StrokeInfo stroke = default,
        List<Vector2D>? points = null)
    {
        Type = type; Bounds = bounds; Color = color; CornerRadius = cornerRadius; ZIndex = zIndex;
        Texture = texture; SourceRect = sourceRect; BlurFilter = blurFilter;
        Font = font; FontSize = fontSize; TextString = text; Stroke = stroke; Points = points;
    }

    internal static FifoCommand Quad(Rect b, Color c, CornerRadius r, float z) => new(0, b, c, r, z);
    internal static FifoCommand Image(Texture t, Rect d, Rect? s, Color c, BlurFilter? b, float z, CornerRadius r = default) =>
        new(1, d, c, r, z, texture: t, sourceRect: s, blurFilter: b);
    internal static FifoCommand MakeText(Font? f, float fontSize, string t, Rect b, Color c, float z) =>
        new(2, b, c, CornerRadius.Zero, z, font: f, fontSize: fontSize, text: t);
    internal static FifoCommand ClipPush(Rect b) => new(3, b, default, CornerRadius.Zero, 0);
    internal static FifoCommand ClipPop() => new(4, default, default, CornerRadius.Zero, 0);
    internal static FifoCommand BlurRegion(Rect b, Color c, CornerRadius r, BlurFilter f) => new(5, b, c, r, 0, blurFilter: f);
    internal static FifoCommand StrokeCmd(Rect b, StrokeInfo s, CornerRadius r, float z) => new(6, b, s.Color, r, z, stroke: s);
    internal static FifoCommand Polygon(List<Vector2D> pts, Color c, float z) => new(7, default, c, CornerRadius.Zero, z, points: pts);
}

/// <summary>
/// Execution context passed down scene graph elements to submit draw commands.
/// All commands are stored in a single FIFO list preserving traversal order.
/// </summary>
public class RenderContext
{
    private readonly List<FifoCommand> _commands = new();
    private readonly Stack<Rect> _visibleStack = new();
    private Rect _visibleBounds;

    internal unsafe SDL_Renderer* RendererHandle { get; set; }

    public IReadOnlyList<FifoCommand> Commands => _commands;

    /// <summary>Must be set each frame to the full window rect before traversal.</summary>
    public Rect VisibleBounds
    {
        get => _visibleBounds;
        set { _visibleStack.Clear(); _visibleBounds = value; }
    }

    /// <summary>True if the given rect intersects the current visible area.</summary>
    public bool IsVisible(Rect rect) =>
        rect.X < _visibleBounds.X + _visibleBounds.Width &&
        rect.X + rect.Width > _visibleBounds.X &&
        rect.Y < _visibleBounds.Y + _visibleBounds.Height &&
        rect.Y + rect.Height > _visibleBounds.Y;

    public unsafe void PushClipRect(Rect bounds)
    {
        _commands.Add(FifoCommand.ClipPush(bounds));
        _visibleStack.Push(_visibleBounds);
        _visibleBounds = Intersect(_visibleBounds, bounds);
    }

    public unsafe void PopClipRect()
    {
        _commands.Add(FifoCommand.ClipPop());
        _visibleBounds = _visibleStack.Count > 0 ? _visibleStack.Pop() : _visibleBounds;
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        float x = Math.Max(a.X, b.X);
        float y = Math.Max(a.Y, b.Y);
        float w = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - x);
        float h = Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - y);
        return new Rect(x, y, w, h);
    }

    public void DrawQuad(Rect bounds, Color color, CornerRadius cornerRadius = default, float zIndex = 1.0f)
    {
        _commands.Add(FifoCommand.Quad(bounds, color, cornerRadius, zIndex));
    }

    /// <summary>
    /// Fills a closed polygon defined by <paramref name="points"/> (in boundary order).
    /// Concave shapes are supported via ear-clipping; degenerate shapes render nothing.
    /// </summary>
    public void DrawPolygon(List<Vector2D> points, Color color, float zIndex = 1.0f)
    {
        if (points == null || points.Count < 3) return;
        _commands.Add(FifoCommand.Polygon(points, color, zIndex));
    }

    public void DrawText(Font? font, float fontSize, string text, Rect bounds, Color color, float zIndex = 1f)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _commands.Add(FifoCommand.MakeText(font, fontSize, text, bounds, color, zIndex));
        }
    }

    public void DrawText(Font? font, string text, Rect bounds, Color color, float zIndex = 1f)
    {
        DrawText(font, font?.DefaultSize ?? 16.0f, text, bounds, color, zIndex);
    }

    public void DrawImage(Texture? texture, Rect destBounds, Rect? sourceRect = null, Color? color = null, BlurFilter? blurFilter = null, float zIndex = 1f, CornerRadius cornerRadius = default)
    {
        if (texture != null && texture.IsValid)
        {
            _commands.Add(FifoCommand.Image(texture, destBounds, sourceRect, color ?? Color.White, blurFilter, zIndex, cornerRadius));
        }
    }

    /// <summary>Captures the current screen region, blurs it, and composites back.</summary>
    public void ApplyBlur(Rect bounds, BlurFilter filter, Color? tint = null, CornerRadius cornerRadius = default)
    {
        if (filter?.Enabled == true && filter.Radius > 0.05f)
            _commands.Add(FifoCommand.BlurRegion(bounds, tint ?? Color.White, cornerRadius, filter));
    }

    public void DrawStroke(Rect bounds, StrokeInfo stroke, CornerRadius cornerRadius = default, float zIndex = 1f)
    {
        if (stroke.Thickness <= 0f || stroke.Color.A <= 0f) return;
        _commands.Add(FifoCommand.StrokeCmd(bounds, stroke, cornerRadius, zIndex));
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
            _commands.Add(FifoCommand.Quad(bounds, color, CornerRadius.Zero, zIndex));
        }
    }
}
