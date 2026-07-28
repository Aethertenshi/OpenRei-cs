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
/// A text rendering UI element.
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

    public override void Render(RenderContext context)
    {
        if (!Visible || string.IsNullOrEmpty(Text)) return;

        // Draw background if color is non-transparent
        base.Render(context);

        // Submit text render command
        context.DrawText(Font, Text, AbsoluteBounds, TextColor, ZIndex);
    }
}
