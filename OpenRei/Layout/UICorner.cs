using OpenRei.Elements;
using OpenRei.Types;

namespace OpenRei.Layout;

/// <summary>
/// Dynamically calculates and applies corner radius rounding (via scale or absolute offset) to an element.
/// </summary>
public class UICorner : LayoutModifier
{
    public UDim CornerRadius { get; set; } = UDim.FromOffset(8f);

    public UICorner() { }

    public UICorner(UDim radius)
    {
        CornerRadius = radius;
    }

    public UICorner(float offset)
    {
        CornerRadius = UDim.FromOffset(offset);
    }

    public override void UpdateLayout(Element element)
    {
        if (!Enabled) return;
        float minDim = MathF.Min(element.AbsoluteSize.X, element.AbsoluteSize.Y);
        element.CornerRadius = CornerRadius.GetAbsolute(minDim);
    }
}
