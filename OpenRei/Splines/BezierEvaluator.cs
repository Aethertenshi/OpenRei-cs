using OpenRei.Types;

namespace OpenRei.Splines;

/// <summary>
/// Mathematical evaluator for Quadratic and Cubic Bézier curves.
/// </summary>
public static class BezierEvaluator
{
    public static Vector2D EvaluateQuadratic(Vector2D p0, Vector2D p1, Vector2D p2, float t)
    {
        float u = 1.0f - t;
        return (u * u * p0) + (2.0f * u * t * p1) + (t * t * p2);
    }

    public static Vector2D EvaluateCubic(Vector2D p0, Vector2D p1, Vector2D p2, Vector2D p3, float t)
    {
        float u = 1.0f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return (uuu * p0) + (3.0f * uu * t * p1) + (3.0f * u * tt * p2) + (ttt * p3);
    }

    public static List<Vector2D> GenerateCubicPoints(Vector2D p0, Vector2D p1, Vector2D p2, Vector2D p3, int segments = 32)
    {
        var points = new List<Vector2D>(segments + 1);
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            points.Add(EvaluateCubic(p0, p1, p2, p3, t));
        }
        return points;
    }

    /// <summary>
    /// Evaluates a cubic Bézier from <see cref="UDim2"/> control points. The curve is evaluated
    /// in UDim2 space (scale and offset interpolate independently), which is mathematically
    /// equivalent to resolving controls and evaluating in pixels. Returns the curve as UDim2 points.
    /// </summary>
    public static UDim2 EvaluateCubic(UDim2 p0, UDim2 p1, UDim2 p2, UDim2 p3, float t)
    {
        float u = 1.0f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return (p0 * uuu) + (p1 * (3.0f * uu * t)) + (p2 * (3.0f * u * tt)) + (p3 * ttt);
    }

    /// <summary>
    /// Generates the sampled points of a cubic Bézier from <see cref="UDim2"/> control points,
    /// returning UDim2 points (resolution-independent).
    /// </summary>
    public static List<UDim2> GenerateCubicPoints(UDim2 p0, UDim2 p1, UDim2 p2, UDim2 p3, int segments = 32)
    {
        var points = new List<UDim2>(segments + 1);
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            points.Add(EvaluateCubic(p0, p1, p2, p3, t));
        }
        return points;
    }
}
