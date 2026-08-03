using System.Runtime.CompilerServices;
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
/// Automatically positions and anchors child elements in a horizontal or vertical list with padding and alignment controls.
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
    /// Stable per-child manual offsets (cross-axis freedom). Captured on first layout so the
    /// auto layout can override scale while preserving the user's offset without accumulating
    /// feedback. Entries are garbage-collected automatically with their child.
    /// </summary>
    private readonly ConditionalWeakTable<Element, ManualOffset> _manualOffsets = new();

    private sealed class ManualOffset
    {
        public float X;
        public float Y;
    }

    /// <summary>
    /// Computes the total content size occupied by children in the list.
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

        return new Vector2D(totalX + padLeft + padRight, totalY + padTop + padBot);
    }

    public override void UpdateLayout(Element parent)
    {
        if (!Enabled || parent.Children.Count == 0) return;

        var children = parent.GetSortedChildren();
        Vector2D parentSize = parent.AbsoluteSize;
        if (parentSize.X <= 0f && parentSize.Y <= 0f) return;

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
                // Stable manual X offset (captured once, not re-read from the overwritten Position)
                float userX = _manualOffsets.GetValue(child, static c =>
                    new ManualOffset { X = c.Position.X.Offset, Y = c.Position.Y.Offset }).X;

                UDim posX;
                float anchorX;

                switch (HorizontalAlignment)
                {
                    case HorizontalAlignment.Right:
                        anchorX = 1.0f;
                        posX = new UDim(1.0f, -padRight + userX);
                        break;
                    case HorizontalAlignment.Center:
                        anchorX = 0.5f;
                        posX = new UDim(0.5f, userX);
                        break;
                    case HorizontalAlignment.Left:
                    default:
                        anchorX = 0.0f;
                        posX = new UDim(0.0f, padLeft + userX);
                        break;
                }

                child.Anchor = new Anchor(anchorX, 0.0f);
                child.Position = new UDim2(posX, UDim.FromOffset(currentOffset));
                currentOffset += childSize.Y + betweenPx;
            }
            else // Horizontal
            {
                // Stable manual Y offset (captured once, not re-read from the overwritten Position)
                float userY = _manualOffsets.GetValue(child, static c =>
                    new ManualOffset { X = c.Position.X.Offset, Y = c.Position.Y.Offset }).Y;

                UDim posY;
                float anchorY;

                switch (VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        anchorY = 1.0f;
                        posY = new UDim(1.0f, -padBot + userY);
                        break;
                    case VerticalAlignment.Center:
                        anchorY = 0.5f;
                        posY = new UDim(0.5f, userY);
                        break;
                    case VerticalAlignment.Top:
                    default:
                        anchorY = 0.0f;
                        posY = new UDim(0.0f, padTop + userY);
                        break;
                }

                child.Anchor = new Anchor(0.0f, anchorY);
                child.Position = new UDim2(UDim.FromOffset(currentOffset), posY);
                currentOffset += childSize.X + betweenPx;
            }
        }
    }
}
