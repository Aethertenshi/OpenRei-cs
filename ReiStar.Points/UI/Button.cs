namespace reistar.Points.UI;

using System;
using System.Collections.Generic;
using reistar.Maths;
using reistar.Graphics;
using reistar.Shapes;

public class Button : UIElement
{
    private string _text = string.Empty;
    private Font? _font;
    private float _fontSize = 16f;
    private Color _textColor = Color.White;
    private Color _hoverColor = Color.Transparent;
    private Color _pressedColor = Color.Transparent;

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                MarkDirty();
            }
        }
    }

    public Font? Font
    {
        get => _font;
        set
        {
            if (_font != value)
            {
                _font = value;
                MarkDirty();
            }
        }
    }

    public float FontSize
    {
        get => _fontSize;
        set
        {
            if (_fontSize != value)
            {
                _fontSize = value;
                MarkDirty();
            }
        }
    }

    public Color TextColor
    {
        get => _textColor;
        set => _textColor = value;
    }

    public Color HoverColor
    {
        get => _hoverColor;
        set => _hoverColor = value;
    }

    public Color PressedColor
    {
        get => _pressedColor;
        set => _pressedColor = value;
    }

    public List<Vect2D>? CustomPoints { get; set; }

    /// <summary>
    /// Event fired when the button is clicked.
    /// </summary>
    public new event Action? OnClick;

    public Button()
    {
        InterceptsMouse = true;
        Size = UVect.FromOffset(120, 40);
        BackgroundColor = new Color(40, 40, 55, 255);
        HoverColor = new Color(60, 60, 85, 255);
        PressedColor = new Color(30, 30, 45, 255);
        base.OnClick = (_) => OnClick?.Invoke();
    }

    public Button(string text, Font? font = null, float fontSize = 16f, Action? onClick = null)
        : this()
    {
        _text = text;
        _font = font;
        _fontSize = fontSize;
        if (onClick != null)
        {
            OnClick += onClick;
        }
    }

    public override void Draw(IRenderer renderer)
    {
        if (!Visible || SkipDraw) return;

        int effectiveZIndex = (CalculatedDepth * 10) + ZIndex;

        Color currentBg = BackgroundColor;
        if (IsPressed && PressedColor.A > 0)
        {
            currentBg = PressedColor;
        }
        else if (IsHovered && HoverColor.A > 0)
        {
            currentBg = HoverColor;
        }

        if (currentBg.A > 0)
        {
            Shapes.DrawRect(renderer, ResolvedTopLeft, ResolvedSize, currentBg, Anchor.TopLeft, effectiveZIndex);
        }

        if (BorderColor.A > 0 && BorderThickness > 0)
        {
            Shapes.DrawRectOutline(renderer, ResolvedTopLeft, ResolvedSize, BorderThickness, BorderColor, Anchor.TopLeft, effectiveZIndex + 1);
        }

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Draw(renderer);
        }

        if (!string.IsNullOrEmpty(_text) && _font != null)
        {
            Vect2D textSize = _font.MeasureString(_text, _fontSize);
            Vect2D textPos = new Vect2D(
                ResolvedTopLeft.X + (ResolvedSize.X - textSize.X) * 0.5f,
                ResolvedTopLeft.Y + (ResolvedSize.Y - textSize.Y) * 0.5f
            );

            Shapes.DrawText(renderer, _font, _text, textPos, _fontSize, _textColor, Anchor.TopLeft, effectiveZIndex + 2);
        }
    }
}
