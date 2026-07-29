using OpenRei.Elements;
using OpenRei.Types;

namespace OpenRei.Layout;

/// <summary>
/// Applies an outline stroke border to an element with configurable thickness, color, and alignment.
/// </summary>
public class UIStroke : LayoutModifier
{
    public float Thickness { get; set; } = 1f;
    public Color Color { get; set; } = Color.White;
    public StrokeAlignment Alignment { get; set; } = StrokeAlignment.Inside;

    public UIStroke() { }

    public UIStroke(float thickness, Color color, StrokeAlignment alignment = StrokeAlignment.Inside)
    {
        Thickness = thickness;
        Color = color;
        Alignment = alignment;
    }

    public UIStroke(float thickness, string hexColor, StrokeAlignment alignment = StrokeAlignment.Inside)
    {
        Thickness = thickness;
        Color = Color.FromHex(hexColor);
        Alignment = alignment;
    }

    public override void UpdateLayout(Element element)
    {
        if (!Enabled)
        {
            element.Stroke = StrokeInfo.None;
            return;
        }

        element.Stroke = new StrokeInfo(Thickness, Color, Alignment);
    }
}
