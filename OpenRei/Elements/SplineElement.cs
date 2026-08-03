using OpenRei.Graphics;
using OpenRei.Splines;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// A scene element for rendering filled continuous shapes (and, in future, stroked
/// vector paths). Control points are <see cref="UDim2"/>, resolved against the parent
/// size so the shape scales with its container. Position places the shape's anchor
/// point (see <see cref="Anchor"/>); the shape's bounds and pivot are cached and only
/// re-evaluated when the anchor or the parent size changes.
/// </summary>
public class SplineElement : Element
{
    public SplineType Type { get; set; } = SplineType.CubicBezier;
    public List<UDim2> ControlPoints { get; init; } = new();
    public float StrokeWidth { get; set; } = 2.0f;
    public Color StrokeColor { get; set; } = Color.White;
    public int SegmentResolution { get; set; } = 32;

    /// <summary>When true, fills the closed outline (ear-clipped). Enabled by default.</summary>
    public bool Filled { get; set; } = true;
    public Color FillColor { get; set; } = Color.White;

    // ── Cached shape metrics (re-evaluated on anchor/parent-size change only) ──
    private readonly List<Vector2D> _resolved = new();
    private readonly List<Vector2D> _renderScratch = new();
    private Vector2D _boundsMin = Vector2D.Zero;
    private Vector2D _boundsMax = Vector2D.Zero;
    private Vector2D _cachedParentSize;
    private bool _shapeDirty = true;

    public SplineElement()
    {
        Name = nameof(SplineElement);
        Color = Color.Transparent;
    }

    public override Anchor Anchor
    {
        get => base.Anchor;
        set
        {
            base.Anchor = value;
            _shapeDirty = true;
        }
    }

    public override Vector2D AbsoluteSize
    {
        get
        {
            EnsureShapeMetrics();
            return _boundsMax - _boundsMin;
        }
    }

    public override Vector2D AbsolutePosition
    {
        get
        {
            Vector2D parentPos = Parent?.AbsolutePosition ?? Vector2D.Zero;
            Vector2D parentSize = Parent?.AbsoluteSize ?? Vector2D.Zero;
            Vector2D relativePos = Position.GetAbsolute(parentSize);
            return parentPos + relativePos - GetPivot();
        }
    }

    private void EnsureShapeMetrics()
    {
        Vector2D ps = Parent?.AbsoluteSize ?? Vector2D.Zero;
        if (!_shapeDirty && ps.X == _cachedParentSize.X && ps.Y == _cachedParentSize.Y)
            return;

        _resolved.Clear();
        foreach (var cp in ControlPoints)
            _resolved.Add(cp.GetAbsolute(ps));

        if (Type == SplineType.CubicBezier && _resolved.Count >= 4)
        {
            var curve = BezierEvaluator.GenerateCubicPoints(
                _resolved[0], _resolved[1], _resolved[2], _resolved[3], SegmentResolution);
            _resolved.Clear();
            _resolved.AddRange(curve);
        }

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < _resolved.Count; i++)
        {
            var p = _resolved[i];
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        if (_resolved.Count < 3)
        {
            _boundsMin = Vector2D.Zero;
            _boundsMax = Vector2D.Zero;
        }
        else
        {
            _boundsMin = new Vector2D(minX, minY);
            _boundsMax = new Vector2D(maxX, maxY);
        }

        _cachedParentSize = ps;
        _shapeDirty = false;
    }

    /// <summary>The anchor point within the shape's bounds, in local coordinates.</summary>
    private Vector2D GetPivot()
    {
        EnsureShapeMetrics();
        return new Vector2D(
            _boundsMin.X + (_boundsMax.X - _boundsMin.X) * Anchor.X,
            _boundsMin.Y + (_boundsMax.Y - _boundsMin.Y) * Anchor.Y);
    }

    /// <summary>Returns the shape's resolved points (parent-size scaled), as a fresh list.</summary>
    public List<Vector2D> GenerateEvaluatedPoints()
    {
        EnsureShapeMetrics();
        return new List<Vector2D>(_resolved);
    }

    public override void Render(RenderContext context)
    {
        if (!Visible) return;

        EnsureShapeMetrics();

        if (Filled && _resolved.Count >= 3 && context.IsVisible(AbsoluteBounds))
        {
            var origin = AbsolutePosition;
            _renderScratch.Clear();
            for (int i = 0; i < _resolved.Count; i++)
                _renderScratch.Add(_resolved[i] + origin);
            context.DrawPolygon(_renderScratch, FillColor, ZIndex);
        }

        base.Render(context);
    }
}
