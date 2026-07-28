using OpenRei.Graphics;
using OpenRei.InputSystem;
using OpenRei.Layout;
using OpenRei.Types;

namespace OpenRei.Elements;

public enum ScrollbarPlacement
{
    Hidden,
    Start,
    End
}

public class ScrollingFrame : Element
{
    private readonly UIListLayout _internalLayout = new();
    private Vector2D _scrollPosition;
    private Vector2D _targetScrollPosition;
    private Vector2D _contentSize;

    public UIListLayout ActiveLayout => Modifiers.OfType<UIListLayout>().LastOrDefault(l => l != _internalLayout) ?? Modifiers.OfType<UIListLayout>().FirstOrDefault() ?? _internalLayout;

    public FillDirection FillDirection
    {
        get => ActiveLayout.FillDirection;
        set => ActiveLayout.FillDirection = value;
    }

    public UDim PaddingTop { get => ActiveLayout.PaddingTop; set => ActiveLayout.PaddingTop = value; }
    public UDim PaddingBottom { get => ActiveLayout.PaddingBottom; set => ActiveLayout.PaddingBottom = value; }
    public UDim PaddingLeft { get => ActiveLayout.PaddingLeft; set => ActiveLayout.PaddingLeft = value; }
    public UDim PaddingRight { get => ActiveLayout.PaddingRight; set => ActiveLayout.PaddingRight = value; }
    public UDim PaddingBetween { get => ActiveLayout.PaddingBetween; set => ActiveLayout.PaddingBetween = value; }
    public UDim Padding { set => ActiveLayout.Padding = value; }

    public float ScrollSmoothness { get; set; } = 14f;
    public float ScrollSpeed { get; set; } = 40f;
    public bool ElasticOvershoot { get; set; } = true;

    public ScrollbarPlacement VerticalScrollbarPosition { get; set; } = ScrollbarPlacement.End;
    public ScrollbarPlacement HorizontalScrollbarPosition { get; set; } = ScrollbarPlacement.Hidden;
    public float ScrollbarThickness { get; set; } = 6f;
    public Color ScrollbarColor { get; set; } = Color.White.WithAlpha(0.5f);
    public Color ScrollbarBackgroundColor { get; set; } = Color.Transparent;

    public Vector2D ContentSize => _contentSize;
    public Vector2D ScrollPosition => _scrollPosition;

    public bool IsScrollableY => _contentSize.Y > AbsoluteSize.Y;
    public bool IsScrollableX => _contentSize.X > AbsoluteSize.X;

    public ScrollingFrame()
    {
        Name = nameof(ScrollingFrame);
        Color = Color.Transparent;
        ClipsToBounds = true;
        Layout = _internalLayout;
    }

    public override void Update(float deltaTime)
    {
        Vector2D viewSize = AbsoluteSize;
        ActiveLayout.UpdateLayout(this);
        _contentSize = ActiveLayout.GetContentSize(this);

        float maxScrollX = MathF.Max(0f, _contentSize.X - viewSize.X);
        float maxScrollY = MathF.Max(0f, _contentSize.Y - viewSize.Y);

        Vector2D mouseDelta = Input.MouseWheelDelta;

        if (mouseDelta.Y != 0f && FillDirection == FillDirection.Vertical)
        {
            float ny = _targetScrollPosition.Y - mouseDelta.Y * ScrollSpeed;
            if (ElasticOvershoot)
                _targetScrollPosition = new Vector2D(_targetScrollPosition.X, ny);
            else if (ny >= 0f && ny <= maxScrollY)
                _targetScrollPosition = new Vector2D(_targetScrollPosition.X, ny);
        }
        else if (mouseDelta.X != 0f && FillDirection == FillDirection.Horizontal)
        {
            float nx = _targetScrollPosition.X - mouseDelta.X * ScrollSpeed;
            if (ElasticOvershoot)
                _targetScrollPosition = new Vector2D(nx, _targetScrollPosition.Y);
            else if (nx >= 0f && nx <= maxScrollX)
                _targetScrollPosition = new Vector2D(nx, _targetScrollPosition.Y);
        }

        if (!ElasticOvershoot)
        {
            _targetScrollPosition = new Vector2D(
                MathF.Max(0f, MathF.Min(MathF.Max(0f, maxScrollX), _targetScrollPosition.X)),
                MathF.Max(0f, MathF.Min(MathF.Max(0f, maxScrollY), _targetScrollPosition.Y))
            );
        }

        float t = 1f - MathF.Exp(-ScrollSmoothness * deltaTime);
        _scrollPosition += (_targetScrollPosition - _scrollPosition) * t;

        if (ElasticOvershoot)
        {
            float elasticPull = 1f - MathF.Exp(-4f * deltaTime);

            float clampedX = MathF.Max(0f, MathF.Min(maxScrollX, _targetScrollPosition.X));
            float clampedY = MathF.Max(0f, MathF.Min(maxScrollY, _targetScrollPosition.Y));

            _targetScrollPosition = new Vector2D(
                _targetScrollPosition.X + (clampedX - _targetScrollPosition.X) * elasticPull,
                _targetScrollPosition.Y + (clampedY - _targetScrollPosition.Y) * elasticPull
            );
        }

        ApplyScrollToChildren();

        Tick(deltaTime);

        foreach (var child in Children)
        {
            if (child.Visible)
                child.Update(deltaTime);
        }
    }

    private void ApplyScrollToChildren()
    {
        foreach (var child in Children)
        {
            if (!child.Visible) continue;

            UDim2 pos = child.Position;
            if (FillDirection == FillDirection.Vertical)
            {
                float naturalY = pos.Y.Offset;
                child.Position = new UDim2(pos.X, new UDim(0f, naturalY - _scrollPosition.Y));
            }
            else
            {
                float naturalX = pos.X.Offset;
                child.Position = new UDim2(new UDim(0f, naturalX - _scrollPosition.X), pos.Y);
            }
        }
    }

    public override void Render(RenderContext context)
    {
        if (!Visible) return;

        if (Color.A > 0f && AbsoluteSize.X > 0f && AbsoluteSize.Y > 0f)
            context.DrawQuad(AbsoluteBounds, Color, CornerRadius, ZIndex);

        context.PushClipRect(AbsoluteBounds);

        var sortedChildren = GetSortedChildren();
        foreach (var child in sortedChildren)
        {
            if (child.Visible)
                child.Render(context);
        }

        context.PopClipRect();

        RenderScrollbars(context);
    }

    private void RenderScrollbars(RenderContext context)
    {
        Vector2D viewSize = AbsoluteSize;
        float viewX = AbsolutePosition.X;
        float viewY = AbsolutePosition.Y;

        if (VerticalScrollbarPosition != ScrollbarPlacement.Hidden && IsScrollableY)
        {
            float barHeight = (viewSize.Y / _contentSize.Y) * viewSize.Y;
            float barY = viewY + (_scrollPosition.Y / MathF.Max(1f, _contentSize.Y - viewSize.Y)) * (viewSize.Y - barHeight);

            float barX = VerticalScrollbarPosition == ScrollbarPlacement.End
                ? viewX + viewSize.X - ScrollbarThickness
                : viewX;

            if (ScrollbarBackgroundColor.A > 0f)
                context.DrawQuad(new Rect(barX, viewY, ScrollbarThickness, viewSize.Y), ScrollbarBackgroundColor, 0f, ZIndex + 1000);

            context.DrawQuad(new Rect(barX, barY, ScrollbarThickness, barHeight), ScrollbarColor, ScrollbarThickness / 2f, ZIndex + 1001);
        }

        if (HorizontalScrollbarPosition != ScrollbarPlacement.Hidden && IsScrollableX)
        {
            float barWidth = (viewSize.X / _contentSize.X) * viewSize.X;
            float barX = viewX + (_scrollPosition.X / MathF.Max(1f, _contentSize.X - viewSize.X)) * (viewSize.X - barWidth);

            float barY = HorizontalScrollbarPosition == ScrollbarPlacement.End
                ? viewY + viewSize.Y - ScrollbarThickness
                : viewY;

            if (ScrollbarBackgroundColor.A > 0f)
                context.DrawQuad(new Rect(viewX, barY, viewSize.X, ScrollbarThickness), ScrollbarBackgroundColor, 0f, ZIndex + 1000);

            context.DrawQuad(new Rect(barX, barY, barWidth, ScrollbarThickness), ScrollbarColor, ScrollbarThickness / 2f, ZIndex + 1001);
        }
    }
}
