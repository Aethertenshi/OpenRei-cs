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
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

    public UDim PaddingTop { get; set; } = UDim.Zero;
    public UDim PaddingBottom { get; set; } = UDim.Zero;
    public UDim PaddingLeft { get; set; } = UDim.Zero;
    public UDim PaddingRight { get; set; } = UDim.Zero;
    public UDim PaddingBetween { get; set; } = UDim.Zero;

    public UDim Padding
    {
        set
        {
            PaddingTop = value;
            PaddingBottom = value;
            PaddingLeft = value;
            PaddingRight = value;
            PaddingBetween = value;
        }
    }

    /// <summary>
    /// Computes the total content size that would be occupied by children after layout.
    /// </summary>
    public Vector2D GetContentSize(Element parent)
    {
        if (parent.Children.Count == 0) return Vector2D.Zero;

        var children = parent.GetSortedChildren();
        float totalX = 0f;
        float totalY = 0f;
        float betweenPx = FillDirection == FillDirection.Vertical
            ? PaddingBetween.GetAbsolute(parent.AbsoluteSize.Y)
            : PaddingBetween.GetAbsolute(parent.AbsoluteSize.X);

        bool first = true;
        foreach (var child in children)
        {
            if (!child.Visible) continue;

            Vector2D childSize = child.AbsoluteSize;

            if (FillDirection == FillDirection.Vertical)
            {
                if (!first) totalY += betweenPx;
                totalY += childSize.Y;
                totalX = MathF.Max(totalX, childSize.X);
                first = false;
            }
            else
            {
                if (!first) totalX += betweenPx;
                totalX += childSize.X;
                totalY = MathF.Max(totalY, childSize.Y);
                first = false;
            }
        }

        float padTop = PaddingTop.GetAbsolute(parent.AbsoluteSize.Y);
        float padBot = PaddingBottom.GetAbsolute(parent.AbsoluteSize.Y);
        float padLeft = PaddingLeft.GetAbsolute(parent.AbsoluteSize.X);
        float padRight = PaddingRight.GetAbsolute(parent.AbsoluteSize.X);

        if (FillDirection == FillDirection.Vertical)
            return new Vector2D(totalX + padLeft + padRight, totalY + padTop + padBot);
        else
            return new Vector2D(totalX + padLeft + padRight, totalY + padTop + padBot);
    }

    public override void UpdateLayout(Element parent)
    {
        if (!Enabled || parent.Children.Count == 0) return;

        var children = parent.GetSortedChildren();
        Vector2D parentSize = parent.AbsoluteSize;
        float betweenPx = FillDirection == FillDirection.Vertical
            ? PaddingBetween.GetAbsolute(parentSize.Y)
            : PaddingBetween.GetAbsolute(parentSize.X);

        float padTop = PaddingTop.GetAbsolute(parentSize.Y);
        float padBot = PaddingBottom.GetAbsolute(parentSize.Y);
        float padLeft = PaddingLeft.GetAbsolute(parentSize.X);
        float padRight = PaddingRight.GetAbsolute(parentSize.X);

        float currentOffset = FillDirection == FillDirection.Vertical ? padTop : padLeft;

        foreach (var child in children)
        {
            if (!child.Visible) continue;

            Vector2D childSize = child.AbsoluteSize;

            if (FillDirection == FillDirection.Vertical)
            {
                float userX = child.Position.X.Offset;
                child.Position = new UDim2(
                    new UDim(0f, padLeft + userX),
                    UDim.FromOffset(currentOffset)
                );
                currentOffset += childSize.Y + betweenPx;
            }
            else
            {
                float userY = child.Position.Y.Offset;
                child.Position = new UDim2(
                    UDim.FromOffset(currentOffset),
                    new UDim(0f, padTop + userY)
                );
                currentOffset += childSize.X + betweenPx;
            }
        }
    }
}
