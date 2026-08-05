namespace reistar.Points.UI;

using reistar.Maths;
using reistar.Graphics;
using reistar.Shapes;

public class Label : UIElement
{
    public string Text { get; set; } = string.Empty;
    public Font? Font { get; set; }
    public float FontSize { get; set; } = 20f;
    public Color TextColor { get; set; } = Color.White;

    public Label()
    {
        Layout = LayoutMode.None;
    }

    public Label(string text, Font? font = null, float fontSize = 20f, Color textColor = default)
    {
        Text = text;
        Font = font;
        FontSize = fontSize;
        TextColor = textColor.A == 0 ? Color.White : textColor;
        Layout = LayoutMode.None;
    }

    public override void Draw(IRenderer renderer)
    {
        base.Draw(renderer);

        if (!string.IsNullOrEmpty(Text) && Font != null)
        {
            int effectiveZIndex = (CalculatedDepth * 10) + ZIndex + 1;
            Shapes.DrawText(renderer, Font, Text, ResolvedTopLeft, FontSize, TextColor, Anchor.TopLeft, effectiveZIndex);
        }
    }
}
