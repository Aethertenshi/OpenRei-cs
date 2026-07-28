using OpenRei.Elements;
using OpenRei.Types;

namespace OpenRei.Layout;

/// <summary>
/// Dynamically calculates and applies per-corner rounding (TopLeft, TopRight, BottomLeft, BottomRight) to an element.
/// </summary>
public class UICorner : LayoutModifier
{
    public UDim TopLeft { get; set; } = UDim.Zero;
    public UDim TopRight { get; set; } = UDim.Zero;
    public UDim BottomLeft { get; set; } = UDim.Zero;
    public UDim BottomRight { get; set; } = UDim.Zero;

    public UDim CornerRadius
    {
        set
        {
            TopLeft = value;
            TopRight = value;
            BottomLeft = value;
            BottomRight = value;
        }
    }

    public UICorner() { }

    public UICorner(float uniformRadius)
    {
        CornerRadius = UDim.FromOffset(uniformRadius);
    }

    public UICorner(UDim uniformRadius)
    {
        CornerRadius = uniformRadius;
    }

    public UICorner(float topLeft, float topRight, float bottomLeft, float bottomRight)
    {
        TopLeft = UDim.FromOffset(topLeft);
        TopRight = UDim.FromOffset(topRight);
        BottomLeft = UDim.FromOffset(bottomLeft);
        BottomRight = UDim.FromOffset(bottomRight);
    }

    public UICorner(UDim topLeft, UDim topRight, UDim bottomLeft, UDim bottomRight)
    {
        TopLeft = topLeft;
        TopRight = topRight;
        BottomLeft = bottomLeft;
        BottomRight = bottomRight;
    }

    public override void UpdateLayout(Element element)
    {
        if (!Enabled) return;
        float minDim = MathF.Min(element.AbsoluteSize.X, element.AbsoluteSize.Y);
        element.CornerRadius = new CornerRadius(
            TopLeft.GetAbsolute(minDim),
            TopRight.GetAbsolute(minDim),
            BottomLeft.GetAbsolute(minDim),
            BottomRight.GetAbsolute(minDim)
        );
    }
}
