namespace reistar.Points.UI;

using System;
using System.Collections.Generic;
using reistar.Maths;
using reistar.Graphics;
using reistar.Shapes;

public abstract class UIElement
{
    public string Id { get; set; } = string.Empty;
    public UVect Position { get; set; } = UVect.FromOffset(0, 0);
    public UVect Size { get; set; } = UVect.FromScale(1f, 1f);
    public Anchor Anchor { get; set; } = Anchor.TopLeft;
    public Color BackgroundColor { get; set; } = Color.Transparent;
    public Color BorderColor { get; set; } = Color.Transparent;
    public int ZIndex { get; set; } = 0;

    public LayoutMode Layout { get; set; } = LayoutMode.None;
    public float Padding { get; set; } = 0f;
    public float Spacing { get; set; } = 0f;

    public UIElement? Parent { get; private set; }
    public List<UIElement> Children { get; } = new();

    public Vect2D ResolvedTopLeft { get; protected set; }
    public Vect2D ResolvedSize { get; protected set; }
    public int CalculatedDepth { get; protected set; }

    public void AddChild(UIElement child)
    {
        if (child.Parent != null)
        {
            child.Parent.RemoveChild(child);
        }
        child.Parent = this;
        Children.Add(child);
    }

    public void RemoveChild(UIElement child)
    {
        if (Children.Remove(child))
        {
            child.Parent = null;
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
                child.Position = UVect.FromOffset(0f, currentY - contentAreaTopLeft.Y);
                child.CalculateLayout(contentAreaSize, contentAreaTopLeft, depth + 1);
                currentY += child.ResolvedSize.Y + Spacing;
            }
        }
        else if (Layout == LayoutMode.HorizontalStack)
        {
            float currentX = contentAreaTopLeft.X;
            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                child.Position = UVect.FromOffset(currentX - contentAreaTopLeft.X, 0f);
                child.CalculateLayout(contentAreaSize, contentAreaTopLeft, depth + 1);
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

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Draw(renderer);
        }
    }
}
