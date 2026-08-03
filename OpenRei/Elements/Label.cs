using OpenRei.Graphics;
using OpenRei.Types;

namespace OpenRei.Elements;

public enum TextAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// A text rendering UI element with automatic text measurement support.
/// </summary>
public class Label : Element
{
    public string Text { get; set; } = string.Empty;
    public Color TextColor { get; set; } = Color.White;
    public float FontSize { get; set; } = 16.0f;
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    public Font? Font { get; set; }

    public Label()
    {
        Name = nameof(Label);
        Color = Color.Transparent;
    }

    /// <summary>
    /// Computes absolute size, auto-measuring text bounds if Size is unassigned or zero.
    /// </summary>
    public override Vector2D AbsoluteSize
    {
        get
        {
            Vector2D userSize = base.AbsoluteSize;
            if (userSize.X > 0f && userSize.Y > 0f) return userSize;

            if (!string.IsNullOrEmpty(Text))
            {
                Font? font = Font ?? FontEngine.DefaultFont;
                if (font != null)
                {
                    Vector2D measured = font.MeasureString(Text, FontSize);
                    return new Vector2D(
                        userSize.X > 0f ? userSize.X : measured.X,
                        userSize.Y > 0f ? userSize.Y : measured.Y
                    );
                }
            }

            return userSize;
        }
    }

    public override void Render(RenderContext context)
    {
        if (!Visible || string.IsNullOrEmpty(Text)) return;

        // Draw background if color is non-transparent
        base.Render(context);

        Font? resolved = Font ?? FontEngine.DefaultFont;
        if (resolved != null)
        {
            context.DrawText(resolved, FontSize, Text, AbsoluteBounds, TextColor, ZIndex);
        }
    }
}
