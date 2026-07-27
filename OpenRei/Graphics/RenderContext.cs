using OpenRei.Types;

namespace OpenRei.Graphics;

/// <summary>
/// Execution context passed down scene graph elements to submit 2D quad instances to the RenderQueue.
/// </summary>
public class RenderContext
{
    private readonly RenderQueue _queue;

    public RenderContext(RenderQueue queue)
    {
        _queue = queue;
    }

    public void DrawQuad(Rect bounds, Color color, float cornerRadius = 0.0f, float zIndex = 1.0f)
    {
        _queue.Enqueue(new QuadInstance(bounds, color, cornerRadius, zIndex));
    }

    public void DrawSpline(List<Vector2D> points, float strokeWidth, Color color, float zIndex = 1.0f)
    {
        if (points.Count < 2) return;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2D p0 = points[i];
            Vector2D p1 = points[i + 1];

            Vector2D diff = p1 - p0;
            float len = diff.Length;
            if (len <= 0.0001f) continue;

            Rect bounds = new Rect(p0.X, p0.Y - strokeWidth * 0.5f, len, strokeWidth);
            _queue.Enqueue(new QuadInstance(bounds, color, 0.0f, zIndex));
        }
    }
}
