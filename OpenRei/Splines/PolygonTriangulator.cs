using OpenRei.Types;

namespace OpenRei.Splines;

/// <summary>
/// Triangulates simple polygons (convex or concave) via the ear-clipping algorithm.
/// Handles degenerate inputs safely: returns false rather than producing garbage.
///
/// Not supported: self-intersecting polygons and polygons with holes.
/// </summary>
public static class PolygonTriangulator
{
    private const float Epsilon = 1e-6f;

    /// <summary>
    /// Triangulates a closed polygon defined by <paramref name="points"/> (in boundary order).
    /// Writes the deduplicated vertices to <paramref name="outVerts"/> and triangle indices
    /// (3 per triangle, referencing <paramref name="outVerts"/>) to <paramref name="outIndices"/>.
    /// Returns false when the polygon cannot be triangulated (degenerate, collinear,
    /// self-intersecting, or fewer than 3 unique points).
    /// </summary>
    public static bool Triangulate(List<Vector2D> points, List<Vector2D> outVerts, List<int> outIndices)
    {
        outVerts.Clear();
        outIndices.Clear();
        if (points == null || points.Count < 3) return false;

        // Collapse consecutive duplicate/coincident points
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (outVerts.Count > 0 && (p - outVerts[^1]).LengthSquared < Epsilon) continue;
            outVerts.Add(p);
        }

        // Collapse a repeated closing point (last == first)
        if (outVerts.Count > 1 && (outVerts[0] - outVerts[^1]).LengthSquared < Epsilon)
            outVerts.RemoveAt(outVerts.Count - 1);

        int n = outVerts.Count;
        if (n < 3) return false;

        // Signed area (shoelace). Zero = degenerate.
        float area = 0f;
        for (int i = 0; i < n; i++)
        {
            var a = outVerts[i];
            var b = outVerts[(i + 1) % n];
            area += a.X * b.Y - b.X * a.Y;
        }
        area *= 0.5f;
        if (Math.Abs(area) < Epsilon) return false;

        // Normalize to CCW so the convex/reflex test is consistent
        if (area < 0f) outVerts.Reverse();

        var remaining = new List<int>(n);
        for (int i = 0; i < n; i++) remaining.Add(i);

        int guard = n * n + n; // safety bound for the ear-clipping loop
        while (remaining.Count > 2 && guard-- > 0)
        {
            bool clipped = false;
            int m = remaining.Count;

            for (int i = 0; i < m; i++)
            {
                int prev = remaining[(i - 1 + m) % m];
                int curr = remaining[i];
                int next = remaining[(i + 1) % m];

                var a = outVerts[prev];
                var b = outVerts[curr];
                var c = outVerts[next];

                // Convex test for CCW: cross(b-a, c-b) must be positive
                float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (cross <= Epsilon) continue;

                // An ear is a convex vertex whose triangle contains no other vertex
                bool ear = true;
                for (int j = 0; j < m; j++)
                {
                    int k = remaining[j];
                    if (k == prev || k == curr || k == next) continue;
                    if (PointInTriangle(outVerts[k], a, b, c))
                    {
                        ear = false;
                        break;
                    }
                }

                if (ear)
                {
                    outIndices.Add(prev);
                    outIndices.Add(curr);
                    outIndices.Add(next);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
            }

            if (!clipped) return false; // no ear found — degenerate / self-intersecting
        }

        return remaining.Count <= 2 && outIndices.Count >= 3;
    }

    private static bool PointInTriangle(Vector2D p, Vector2D a, Vector2D b, Vector2D c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);

        bool hasNeg = d1 < -Epsilon || d2 < -Epsilon || d3 < -Epsilon;
        bool hasPos = d1 > Epsilon || d2 > Epsilon || d3 > Epsilon;
        return !(hasNeg && hasPos);
    }

    private static float Cross(Vector2D a, Vector2D b) => a.X * b.Y - a.Y * b.X;
}
