using OpenRei.Filters;
using OpenRei.Graphics;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// An interactive button element supporting hover, press, click events, built-in state colors, and text display.
/// </summary>
public class Button : Element
{
    public string Text { get; set; } = string.Empty;
    public Color TextColor { get; set; } = Color.Black;
    public float FontSize { get; set; } = 16.0f;
    public Color HoverColor { get; set; } = Color.FromRgba(220, 220, 220, 255);
    public Color PressColor { get; set; } = Color.FromRgba(180, 180, 180, 255);
    public Font? Font { get; set; }

    public bool IsHovered { get; private set; }
    public bool IsPressed { get; private set; }

    public event Action? OnClick;
    public event Action? OnHoverEnter;
    public event Action? OnHoverLeave;

    public Button()
    {
        Name = nameof(Button);
    }

    public override void HandleInput(Vector2D mousePos, bool mousePressed, bool mouseReleased)
    {
        bool contains = AbsoluteBounds.Contains(mousePos);

        if (contains && !IsHovered)
        {
            IsHovered = true;
            OnHoverEnter?.Invoke();
        }
        else if (!contains && IsHovered)
        {
            IsHovered = false;
            OnHoverLeave?.Invoke();
        }

        if (IsHovered && mousePressed)
        {
            IsPressed = true;
        }

        if (IsPressed && mouseReleased)
        {
            IsPressed = false;
            if (contains)
            {
                OnClick?.Invoke();
            }
        }

        base.HandleInput(mousePos, mousePressed, mouseReleased);
    }

    public override void Render(RenderContext context)
    {
        if (!Visible) return;

        // Determine active background color based on interactive state
        Color activeColor = (IsPressed && PressColor.A > 0f)
            ? PressColor
            : ((IsHovered && HoverColor.A > 0f) ? HoverColor : Color);

        // Process filters (drop shadow, etc.) BEFORE drawing the button quad
        foreach (var filter in Filters)
        {
            if (filter is DropShadowFilter dsf && dsf.Enabled && dsf.Color.A > 0.001f)
            {
                Rect shadowBounds = new Rect(
                    AbsoluteBounds.X + dsf.Offset.X,
                    AbsoluteBounds.Y + dsf.Offset.Y,
                    AbsoluteBounds.Width, AbsoluteBounds.Height);

                if (dsf.BlurRadius > 0.5f)
                {
                    var blurFilter = new BlurFilter(dsf.BlurRadius);
                    context.ApplyBlur(shadowBounds, blurFilter, dsf.Color, CornerRadius);
                }
                else
                {
                    context.DrawQuad(shadowBounds, dsf.Color, CornerRadius, ZIndex - 0.5f);
                }
            }
        }

        // Draw button quad with active interactive state color
        if (activeColor.A > 0f && AbsoluteSize.X > 0f && AbsoluteSize.Y > 0f)
        {
            context.DrawQuad(AbsoluteBounds, activeColor, CornerRadius, ZIndex);
        }

        // Draw stroke outline if active
        if (Stroke.Thickness > 0f && Stroke.Color.A > 0f)
            context.DrawStroke(AbsoluteBounds, Stroke, CornerRadius, ZIndex + 0.1f);

        // Submit button text command
        if (!string.IsNullOrEmpty(Text))
        {
            Font? resolved = Font ?? FontEngine.DefaultFont;
            if (resolved != null)
            {
                context.DrawText(resolved, FontSize, Text, AbsoluteBounds, TextColor, ZIndex + 0.2f);
            }
        }

        // Render child elements
        var sortedChildren = GetSortedChildren();
        foreach (var child in sortedChildren)
        {
            if (child.Visible)
            {
                child.Render(context);
            }
        }
    }
}
