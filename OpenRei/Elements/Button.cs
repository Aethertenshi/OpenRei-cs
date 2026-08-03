using OpenRei.Graphics;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// An interactive button element supporting hover, press, click events, optional state colors, and text display.
/// </summary>
public class Button : Element
{
    public string Text { get; set; } = string.Empty;
    public Color TextColor { get; set; } = Color.Black;
    public float FontSize { get; set; } = 16.0f;
    public Color? HoverColor { get; set; } = null;
    public Color? PressColor { get; set; } = null;
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

        Color originalColor = Color;

        // Apply optional state colors if explicitly assigned
        if (IsPressed && PressColor.HasValue)
        {
            Color = PressColor.Value;
        }
        else if (IsHovered && HoverColor.HasValue)
        {
            Color = HoverColor.Value;
        }

        // Render base background, filters, stroke, and child elements (e.g. Sprite bgImage)
        base.Render(context);

        // Restore original color property
        Color = originalColor;

        // Submit button text command on top of background & children
        if (!string.IsNullOrEmpty(Text))
        {
            Font? resolved = Font ?? FontEngine.DefaultFont;
            if (resolved != null)
            {
                context.DrawText(resolved, FontSize, Text, AbsoluteBounds, TextColor, ZIndex + 0.2f);
            }
        }
    }
}
