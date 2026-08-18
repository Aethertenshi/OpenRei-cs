namespace reistar.Points.UI;

using System;
using System.Collections.Generic;
using reistar.Maths;
using reistar.Graphics;
using reistar.Shapes;

public abstract class UIElement
{
    private UVect _position = UVect.FromOffset(0, 0);
    private UVect _size = UVect.FromScale(1f, 1f);
    private Anchor _anchor = Anchor.TopLeft;
    private Color _backgroundColor = Color.Transparent;
    private Color _borderColor = Color.Transparent;
    private int _zIndex = 0;
    private LayoutMode _layout = LayoutMode.None;
    private float _padding = 0f;
    private float _spacing = 0f;
    private bool _isDirty = true;

    public string Id { get; set; } = string.Empty;

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
                // Non-destructive: calculate layout passing stack cursor as containerTopLeft
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
                // Non-destructive: calculate layout passing stack cursor as containerTopLeft
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
        int effectiveZIndex = (CalculatedDepth * 10) + ZIndex;

        if (BackgroundColor.A > 0)
        {
            Shapes.DrawRect(renderer, ResolvedTopLeft, ResolvedSize, BackgroundColor, Anchor.TopLeft, effectiveZIndex);
        }

        if (BorderColor.A > 0)
        {
            Shapes.DrawRectOutline(renderer, ResolvedTopLeft, ResolvedSize, 1f, BorderColor, Anchor.TopLeft, effectiveZIndex + 1);
        }

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Draw(renderer);
        }
    }
}
