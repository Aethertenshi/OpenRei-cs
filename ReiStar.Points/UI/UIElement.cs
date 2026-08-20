namespace reistar.Points.UI;

using System;
using System.Collections.Generic;
using reistar.Maths;
using reistar.Graphics;
using reistar.Shapes;
using reistar.Input;

public abstract class UIElement
{
    private UVect _position = UVect.FromOffset(0, 0);
    private UVect _size = UVect.FromScale(1f, 1f);
    private Anchor _anchor = Anchor.TopLeft;
    private Color _backgroundColor = Color.Transparent;
    private Color _borderColor = Color.Transparent;
    private float _borderThickness = 1f;
    private int _zIndex = 0;
    private LayoutMode _layout = LayoutMode.None;
    private float _padding = 0f;
    private float _spacing = 0f;
    private bool _isDirty = true;
    private bool _visible = true;

    public string Id { get; set; } = string.Empty;

    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible != value)
            {
                _visible = value;
                MarkDirty();
            }
        }
    }

    public bool SkipDraw { get; set; } = false;
    public bool InterceptsMouse { get; set; } = true;
    public bool IsHovered { get; protected set; }
    public bool IsPressed { get; protected set; }

    public Action<UIElement>? OnHover { get; set; }
    public Action<UIElement>? OnHoverLeave { get; set; }
    public Action<UIElement, float, float, MouseButton>? OnMouseDown { get; set; }
    public Action<UIElement, float, float, MouseButton>? OnMouseUp { get; set; }
    public Action<UIElement>? OnClick { get; set; }
    public Action<UIElement, float>? OnUpdate { get; set; }

    public UVect Position
    {
        get => _position;
        set
        {
            _position = value;
            MarkDirty();
        }
    }

    public UVect Size
    {
        get => _size;
        set
        {
            _size = value;
            MarkDirty();
        }
    }

    public Anchor Anchor
    {
        get => _anchor;
        set
        {
            _anchor = value;
            MarkDirty();
        }
    }

    public Color BackgroundColor
    {
        get => _backgroundColor;
        set => _backgroundColor = value;
    }

    public Color BorderColor
    {
        get => _borderColor;
        set => _borderColor = value;
    }

    public float BorderThickness
    {
        get => _borderThickness;
        set => _borderThickness = value;
    }

    public int ZIndex
    {
        get => _zIndex;
        set => _zIndex = value;
    }

    public LayoutMode Layout
    {
        get => _layout;
        set
        {
            _layout = value;
            MarkDirty();
        }
    }

    public float Padding
    {
        get => _padding;
        set
        {
            _padding = value;
            MarkDirty();
        }
    }

    public float Spacing
    {
        get => _spacing;
        set
        {
            _spacing = value;
            MarkDirty();
        }
    }

    public bool IsDirty => _isDirty;

    public UIElement? Parent { get; private set; }
    public List<UIElement> Children { get; } = new();

    public Vect2D ResolvedTopLeft { get; protected set; }
    public Vect2D ResolvedSize { get; protected set; }
    public int CalculatedDepth { get; protected set; }

    public void MarkDirty()
    {
        _isDirty = true;
        Parent?.MarkDirty();
    }

    public void ClearDirty()
    {
        _isDirty = false;
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].ClearDirty();
        }
    }

    public void AddChild(UIElement child)
    {
        if (child.Parent != null)
        {
            child.Parent.RemoveChild(child);
        }
        child.Parent = this;
        Children.Add(child);
        MarkDirty();
    }

    public void RemoveChild(UIElement child)
    {
        if (Children.Remove(child))
        {
            child.Parent = null;
            MarkDirty();
        }
    }

    public bool ContainsPoint(Vect2D pt)
    {
        return pt.X >= ResolvedTopLeft.X && pt.X <= ResolvedTopLeft.X + ResolvedSize.X &&
               pt.Y >= ResolvedTopLeft.Y && pt.Y <= ResolvedTopLeft.Y + ResolvedSize.Y;
    }

    public virtual void Update(float deltaTime)
    {
        if (!_visible) return;

        OnUpdate?.Invoke(this, deltaTime);

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Update(deltaTime);
        }
    }

    public virtual bool ProcessMouseMove(Vect2D mousePos)
    {
        if (!_visible) return false;

        bool handled = false;
        // Process children in reverse order (top-most first)
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i].ProcessMouseMove(mousePos))
            {
                handled = true;
                break;
            }
        }

        bool contains = ContainsPoint(mousePos);
        if (contains && !IsHovered)
        {
            IsHovered = true;
            OnHover?.Invoke(this);
        }
        else if (!contains && IsHovered)
        {
            IsHovered = false;
            OnHoverLeave?.Invoke(this);
        }

        return handled || (contains && InterceptsMouse);
    }

    public virtual bool ProcessMouseDown(Vect2D mousePos, MouseButton button)
    {
        if (!_visible) return false;

        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i].ProcessMouseDown(mousePos, button))
            {
                return true;
            }
        }

        if (ContainsPoint(mousePos) && InterceptsMouse)
        {
            IsPressed = true;
            OnMouseDown?.Invoke(this, mousePos.X, mousePos.Y, button);
            return true;
        }

        return false;
    }

    public virtual bool ProcessMouseUp(Vect2D mousePos, MouseButton button)
    {
        if (!_visible) return false;

        bool wasPressed = IsPressed;
        IsPressed = false;

        for (int i = Children.Count - 1; i >= 0; i--)
        {
            Children[i].ProcessMouseUp(mousePos, button);
        }

        bool contains = ContainsPoint(mousePos);
        if (wasPressed)
        {
            OnMouseUp?.Invoke(this, mousePos.X, mousePos.Y, button);
            if (contains)
            {
                OnClick?.Invoke(this);
            }
            return true;
        }

        return false;
    }

    public virtual void CalculateLayout(Vect2D containerSize, Vect2D containerTopLeft = default, int depth = 0)
    {
        CalculatedDepth = depth;
        ResolvedSize = Size.Resolve(containerSize);

        Vect2D rawPos = Position.Resolve(containerSize);
        ResolvedTopLeft = new Vect2D(
            containerTopLeft.X + rawPos.X - (ResolvedSize.X * Anchor.X),
            containerTopLeft.Y + rawPos.Y - (ResolvedSize.Y * Anchor.Y)
        );

        if (Children.Count == 0) return;

        Vect2D contentAreaTopLeft = new Vect2D(ResolvedTopLeft.X + Padding, ResolvedTopLeft.Y + Padding);
        Vect2D contentAreaSize = new Vect2D(
            Math.Max(0, ResolvedSize.X - (Padding * 2f)),
            Math.Max(0, ResolvedSize.Y - (Padding * 2f))
        );

        if (Layout == LayoutMode.VerticalStack)
        {
            float currentY = contentAreaTopLeft.Y;
            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                Vect2D childTopLeft = new Vect2D(contentAreaTopLeft.X, currentY);
                child.CalculateLayout(contentAreaSize, childTopLeft, depth + 1);
                currentY += child.ResolvedSize.Y + Spacing;
            }
        }
        else if (Layout == LayoutMode.HorizontalStack)
        {
            float currentX = contentAreaTopLeft.X;
            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                Vect2D childTopLeft = new Vect2D(currentX, contentAreaTopLeft.Y);
                child.CalculateLayout(contentAreaSize, childTopLeft, depth + 1);
                currentX += child.ResolvedSize.X + Spacing;
            }
        }
        else
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].CalculateLayout(contentAreaSize, contentAreaTopLeft, depth + 1);
            }
        }
    }

    public virtual void Draw(IRenderer renderer)
    {
        if (!_visible || SkipDraw) return;

        int effectiveZIndex = (CalculatedDepth * 10) + ZIndex;

        if (BackgroundColor.A > 0)
        {
            Shapes.DrawRect(renderer, ResolvedTopLeft, ResolvedSize, BackgroundColor, Anchor.TopLeft, effectiveZIndex);
        }

        if (BorderColor.A > 0 && BorderThickness > 0)
        {
            Shapes.DrawRectOutline(renderer, ResolvedTopLeft, ResolvedSize, BorderThickness, BorderColor, Anchor.TopLeft, effectiveZIndex + 1);
        }

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Draw(renderer);
        }
    }
}
