using OpenRei.Elements;
using OpenRei.Types;

namespace OpenRei.Layout;

public enum FillDirection
{
    Horizontal,
    Vertical
}

public enum HorizontalAlignment
{
    Left,
    Center,
    Right
}

public enum VerticalAlignment
{
    Top,
    Center,
    Bottom
}

/// <summary>
/// Automatically positions child elements in a horizontal or vertical list with padding.
/// </summary>
public class UIListLayout : LayoutModifier
{
    public FillDirection FillDirection { get; set; } = FillDirection.Vertical;
    public UDim Padding { get; set; } = UDim.Zero;
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

    public override void UpdateLayout(Element parent)
    {
        if (!Enabled || parent.Children.Count == 0) return;

        var children = parent.GetSortedChildren();
        Vector2D parentSize = parent.AbsoluteSize;
        float currentOffset = 0f;
        float paddingPx = Padding.GetAbsolute(FillDirection == FillDirection.Vertical ? parentSize.Y : parentSize.X);

        foreach (var child in children)
        {
            if (!child.Visible) continue;

            Vector2D childSize = child.AbsoluteSize;

            if (FillDirection == FillDirection.Vertical)
            {
                child.Position = new UDim2(child.Position.X, UDim.FromOffset(currentOffset));
                currentOffset += childSize.Y + paddingPx;
            }
            else
            {
                child.Position = new UDim2(UDim.FromOffset(currentOffset), child.Position.Y);
                currentOffset += childSize.X + paddingPx;
            }
        }
    }
}
