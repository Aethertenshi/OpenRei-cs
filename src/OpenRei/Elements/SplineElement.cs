using OpenRei.Splines;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// A scene element for rendering continuous curves, vector strokes, and splines.
/// </summary>
public class SplineElement : Element
{
    public SplineType Type { get; set; } = SplineType.CubicBezier;
    public List<Vector2D> ControlPoints { get; init; } = new();
    public float StrokeWidth { get; set; } = 2.0f;
    public Color StrokeColor { get; set; } = Color.White;
    public int SegmentResolution { get; set; } = 32;

    public SplineElement()
    {
        Name = nameof(SplineElement);
        Color = Color.Transparent;
    }

    public List<Vector2D> GenerateEvaluatedPoints()
    {
        if (ControlPoints.Count < 2) return new List<Vector2D>();

        if (Type == SplineType.CubicBezier && ControlPoints.Count >= 4)
        {
            return BezierEvaluator.GenerateCubicPoints(
                ControlPoints[0], ControlPoints[1], ControlPoints[2], ControlPoints[3], SegmentResolution
            );
        }

        // Default linear polyline points
        return new List<Vector2D>(ControlPoints);
    }
}
