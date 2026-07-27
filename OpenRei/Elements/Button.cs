using OpenRei.Graphics;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// An interactive button element supporting hover, press, click events, and text display.
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

        // Render button background quad
        base.Render(context);

        // Submit button text command
        if (!string.IsNullOrEmpty(Text))
        {
            context.DrawText(Font, Text, AbsoluteBounds, TextColor);
        }
    }
}
