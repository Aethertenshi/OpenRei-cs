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
    public float CornerRadius { get; set; } = 0.0f;
    public bool Visible { get; set; } = true;
    public bool ClipsToBounds { get; set; } = false;

    public List<Filter> Filters { get; init; } = new();
    public LayoutModifier? Layout { get; set; }

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

    public Vector2D AbsoluteSize
    {
        get
        {
            Vector2D parentSize = _parent?.AbsoluteSize ?? Vector2D.Zero;
            return Size.GetAbsolute(parentSize);
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
        Layout?.UpdateLayout(this);

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

        // Traverse children in reverse ZIndex order (topmost elements receive input first)
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

        // Draw element quad if non-transparent
        if (Color.A > 0f && AbsoluteSize.X > 0f && AbsoluteSize.Y > 0f)
        {
            context.DrawQuad(AbsoluteBounds, Color, CornerRadius, ZIndex);
        }

        // Submit element filters
        foreach (var filter in Filters)
        {
            if (filter is BlurFilter blurFilter && blurFilter.Enabled)
            {
                context.ApplyBlur(AbsoluteBounds, blurFilter);
            }
        }

        // Render children according to local ZIndex stacking order
        var sortedChildren = GetSortedChildren();
        foreach (var child in sortedChildren)
        {
            child.Render(context);
        }
    }
}
