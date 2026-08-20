namespace reistar.Points.UI;

using System;
using System.Collections.Generic;
using reistar.Maths;
using reistar.Graphics;
using reistar.Shapes;
using reistar.Input;

public class ScrollFrame : UIElement
{
    private float _scrollOffset = 0f;
    private float _targetScroll = 0f;
    private float _maxScroll = 0f;
    private float _scrollSmoothness = 10f;

    public float ScrollOffset
    {
        get => _scrollOffset;
        set
        {
            _scrollOffset = Math.Clamp(value, 0f, _maxScroll);
            _targetScroll = _scrollOffset;
            MarkDirty();
        }
    }

    public float TargetScroll
    {
        get => _targetScroll;
        set
        {
            _targetScroll = Math.Clamp(value, 0f, _maxScroll);
        }
    }

    public float ScrollSmoothness
    {
        get => _scrollSmoothness;
        set => _scrollSmoothness = MathF.Max(1f, value);
    }

    public float MaxScroll => _maxScroll;
    public Color ScrollBarColor { get; set; } = new Color(255, 255, 255, 60);

    public ScrollFrame()
    {
        InterceptsMouse = true;
        Layout = LayoutMode.VerticalStack;
        BackgroundColor = Color.Transparent;
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        // Process mouse wheel scroll when hovered
        if (IsHovered && MathF.Abs(Input.MouseWheel) > 0.01f)
        {
            _targetScroll = Math.Clamp(_targetScroll - (Input.MouseWheel * 50f), 0f, _maxScroll);
        }

        if (MathF.Abs(_targetScroll - _scrollOffset) > 0.1f)
        {
            _scrollOffset += (_targetScroll - _scrollOffset) * MathF.Min(1f, deltaTime * _scrollSmoothness);
            MarkDirty();
        }
        else if (_scrollOffset != _targetScroll)
        {
            _scrollOffset = _targetScroll;
            MarkDirty();
        }
    }

    public override void CalculateLayout(Vect2D containerSize, Vect2D containerTopLeft = default, int depth = 0)
    {
        CalculatedDepth = depth;
        ResolvedSize = Size.Resolve(containerSize);

        Vect2D rawPos = Position.Resolve(containerSize);
        ResolvedTopLeft = new Vect2D(
            containerTopLeft.X + rawPos.X - (ResolvedSize.X * Anchor.X),
            containerTopLeft.Y + rawPos.Y - (ResolvedSize.Y * Anchor.Y)
        );

        if (Children.Count == 0)
        {
            _maxScroll = 0f;
            return;
        }

        Vect2D contentAreaTopLeft = new Vect2D(ResolvedTopLeft.X + Padding, ResolvedTopLeft.Y + Padding);
        Vect2D contentAreaSize = new Vect2D(
            Math.Max(0, ResolvedSize.X - (Padding * 2f)),
            Math.Max(0, ResolvedSize.Y - (Padding * 2f))
        );

        float currentY = contentAreaTopLeft.Y - _scrollOffset;
        float totalHeight = 0f;

        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            Vect2D childTopLeft = new Vect2D(contentAreaTopLeft.X, currentY);
            child.CalculateLayout(contentAreaSize, childTopLeft, depth + 1);

            float itemHeight = child.ResolvedSize.Y;
            currentY += itemHeight + Spacing;
            totalHeight += itemHeight + Spacing;
        }

        if (totalHeight > 0f)
        {
            totalHeight -= Spacing;
        }

        _maxScroll = Math.Max(0f, totalHeight - contentAreaSize.Y);
    }

    public override void Draw(IRenderer renderer)
    {
        if (!Visible || SkipDraw) return;

        int effectiveZIndex = (CalculatedDepth * 10) + ZIndex;

        if (BackgroundColor.A > 0)
        {
            Shapes.DrawRect(renderer, ResolvedTopLeft, ResolvedSize, BackgroundColor, Anchor.TopLeft, effectiveZIndex);
        }

        if (BorderColor.A > 0 && BorderThickness > 0)
        {
            Shapes.DrawRectOutline(renderer, ResolvedTopLeft, ResolvedSize, BorderThickness, BorderColor, Anchor.TopLeft, effectiveZIndex + 1);
        }

        // Draw visible children (viewport culling)
        float clipTop = ResolvedTopLeft.Y;
        float clipBottom = ResolvedTopLeft.Y + ResolvedSize.Y;

        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            float childTop = child.ResolvedTopLeft.Y;
            float childBottom = child.ResolvedTopLeft.Y + child.ResolvedSize.Y;

            if (childBottom >= clipTop && childTop <= clipBottom)
            {
                child.Draw(renderer);
            }
        }

        // Draw sleek vertical scrollbar if content overflows
        if (_maxScroll > 0f && ScrollBarColor.A > 0)
        {
            float trackHeight = Math.Max(10f, ResolvedSize.Y - (Padding * 2f));
            float thumbHeight = Math.Max(24f, trackHeight * (ResolvedSize.Y / (ResolvedSize.Y + _maxScroll)));
            float scrollProgress = _scrollOffset / _maxScroll;
            float thumbY = ResolvedTopLeft.Y + Padding + (scrollProgress * (trackHeight - thumbHeight));
            float barWidth = 3.5f;
            float barX = ResolvedTopLeft.X + ResolvedSize.X - barWidth - 2f;

            Shapes.DrawRect(
                renderer,
                new Vect2D(barX, thumbY),
                new Vect2D(barWidth, thumbHeight),
                ScrollBarColor,
                Anchor.TopLeft,
                effectiveZIndex + 9
            );
        }
    }
}
