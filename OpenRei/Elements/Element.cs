using OpenRei.Filters;
using OpenRei.Graphics;
using OpenRei.Layout;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// Core base class for all scene graph nodes, containers, UI controls, and visual elements.
/// </summary>
public class Element
{
    private Element? _parent;
    private readonly List<Element> _children = new();

    public string Name { get; set; } = nameof(Element);
    public UDim2 Position { get; set; } = UDim2.Zero;
    public UDim2 Size { get; set; } = UDim2.Zero;
    public Anchor Anchor { get; set; } = Anchor.TopLeft;
    public int ZIndex { get; set; } = 1;
    public Color Color { get; set; } = Color.White;
    public CornerRadius CornerRadius { get; set; } = CornerRadius.Zero;
    public bool Visible { get; set; } = true;
    public bool ClipsToBounds { get; set; } = false;

    public List<Filter> Filters { get; init; } = new();

    /// <summary>
    /// Collection of active layout modifiers (e.g. UIAspectRatioConstraint, UICorner, UIListLayout, UIPadding).
    /// </summary>
    public List<LayoutModifier> Modifiers { get; init; } = new();

    /// <summary>
    /// Alias getter/setter for the primary layout modifier.
    /// </summary>
    public LayoutModifier? Layout
    {
        get => Modifiers.FirstOrDefault();
        set
        {
            if (value != null && !Modifiers.Contains(value))
            {
                Modifiers.Add(value);
            }
        }
    }

    public Element? Parent
    {
        get => _parent;
        set
        {
            if (_parent == value) return;
            _parent?._children.Remove(this);
            _parent = value;
            _parent?._children.Add(this);
        }
    }

    public IReadOnlyList<Element> Children => _children;

    public float AspectRatio { get; set; } = 0.0f;
    public DominantAxis AspectAxis { get; set; } = DominantAxis.Height;
    public bool AccountAspectOffset { get; set; } = true;

    public Vector2D AbsoluteSize
    {
        get
        {
            Vector2D parentSize = _parent?.AbsoluteSize ?? Vector2D.Zero;
            Vector2D baseSize = Size.GetAbsolute(parentSize);

            float ratio = AspectRatio;
            DominantAxis axis = AspectAxis;
            bool accountOffset = AccountAspectOffset;

            var aspectConstraint = Modifiers.OfType<UIAspectRatioConstraint>().FirstOrDefault()
                ?? (Layout as UIAspectRatioConstraint);

            if (aspectConstraint != null)
            {
                ratio = aspectConstraint.AspectRatio;
                axis = aspectConstraint.AspectAxis;
                accountOffset = aspectConstraint.AccountOffset;
            }

            if (ratio > 0f)
            {
                if (axis == DominantAxis.Height)
                {
                    float heightForAspect = accountOffset
                        ? baseSize.Y
                        : (parentSize.Y * Size.Y.Scale);

                    float derivedWidth = heightForAspect * ratio;
                    if (!accountOffset) derivedWidth += Size.X.Offset;

                    return new Vector2D(derivedWidth, baseSize.Y);
                }
                else
                {
                    float widthForAspect = accountOffset
                        ? baseSize.X
                        : (parentSize.X * Size.X.Scale);

                    float derivedHeight = widthForAspect / ratio;
                    if (!accountOffset) derivedHeight += Size.Y.Offset;

                    return new Vector2D(baseSize.X, derivedHeight);
                }
            }

            return baseSize;
        }
    }

    public Vector2D AbsolutePosition
    {
        get
        {
            Vector2D parentPos = _parent?.AbsolutePosition ?? Vector2D.Zero;
            Vector2D parentSize = _parent?.AbsoluteSize ?? Vector2D.Zero;
            Vector2D selfSize = AbsoluteSize;
            Vector2D relativePos = Position.GetAbsolute(parentSize);
            Vector2D pivotOffset = new(selfSize.X * Anchor.X, selfSize.Y * Anchor.Y);

            return parentPos + relativePos - pivotOffset;
        }
    }

    public Rect AbsoluteBounds => new(AbsolutePosition.X, AbsolutePosition.Y, AbsoluteSize.X, AbsoluteSize.Y);

    public void AddChild(Element child)
    {
        child.Parent = this;
    }

    public void RemoveChild(Element child)
    {
        if (child.Parent == this)
        {
            child.Parent = null;
        }
    }

    public void AddModifier(LayoutModifier modifier)
    {
        if (modifier != null && !Modifiers.Contains(modifier))
        {
            Modifiers.Add(modifier);
        }
    }

    /// <summary>
    /// Returns children sorted by local ZIndex (Local Stacking Context).
    /// </summary>
    public List<Element> GetSortedChildren()
    {
        return _children.OrderBy(c => c.ZIndex).ToList();
    }

    /// <summary>
    /// Opt-in frame update callback. Override this in custom element subclasses to execute game/element frame logic.
    /// </summary>
    protected virtual void Tick(float deltaTime)
    {
    }

    public virtual void Update(float deltaTime)
    {
        Tick(deltaTime);

        foreach (var modifier in Modifiers)
        {
            if (modifier != null && modifier.Enabled)
            {
                modifier.UpdateLayout(this);
            }
        }

        foreach (var child in _children)
        {
            if (child.Visible)
            {
                child.Update(deltaTime);
            }
        }
    }

    public virtual void HandleInput(Vector2D mousePos, bool mousePressed, bool mouseReleased)
    {
        if (!Visible) return;

        var sortedChildren = _children.OrderByDescending(c => c.ZIndex).ToList();
        foreach (var child in sortedChildren)
        {
            if (child.Visible)
            {
                child.HandleInput(mousePos, mousePressed, mouseReleased);
            }
        }
    }

    public virtual void Render(RenderContext context)
    {
        if (!Visible) return;

        // Process filters BEFORE drawing the element's own content
        foreach (var filter in Filters)
        {
            if (filter is DropShadowFilter dsf && dsf.Enabled)
            {
                // 1. Draw shadow quad at offset with shadow color
                Rect shadowBounds = new Rect(
                    AbsoluteBounds.X + dsf.Offset.X,
                    AbsoluteBounds.Y + dsf.Offset.Y,
                    AbsoluteBounds.Width, AbsoluteBounds.Height);
                context.DrawQuad(shadowBounds, dsf.Color, CornerRadius, ZIndex - 0.5f);

                // 2. Blur the shadow region if radius > 0 (FBO-only, no readback)
                if (dsf.BlurRadius > 0.5f)
                {
                    var blurFilter = new BlurFilter(dsf.BlurRadius);
                    context.ApplyBlur(shadowBounds, blurFilter, dsf.Color, CornerRadius);
                }
            }
        }

        // Draw element quad if non-transparent
        if (Color.A > 0f && AbsoluteSize.X > 0f && AbsoluteSize.Y > 0f)
        {
            context.DrawQuad(AbsoluteBounds, Color, CornerRadius, ZIndex);
        }

        // Render children according to local ZIndex stacking order
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
