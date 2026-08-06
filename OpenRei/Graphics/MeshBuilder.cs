using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// A high-performance mesh accumulator for bulk geometry. Uses grow-only arrays so a
/// reused instance allocates nothing per frame after warm-up. A map element should reuse
/// one <see cref="MeshBuilder"/>, build all its geometry into it, and submit a single
/// <see cref="RenderContext.DrawMesh(RenderContext)"/> per frame for one draw call.
/// </summary>
public class MeshBuilder
{
    private SDL_Vertex[] _verts = new SDL_Vertex[64];
    private int[] _indices = new int[128];

    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }

    /// <summary>Resets the builder for reuse. Safe to call every frame.</summary>
    public void Clear()
    {
        VertexCount = 0;
        IndexCount = 0;
    }

    /// <summary>Adds a filled quad (a→b→c→d around the perimeter).</summary>
    public void AddQuad(Vector2D a, Vector2D b, Vector2D c, Vector2D d, Color color)
    {
        EnsureVerts(VertexCount + 4);
        EnsureIndices(IndexCount + 6);

        int baseIdx = VertexCount;
        var col = new SDL_FColor { r = color.R, g = color.G, b = color.B, a = color.A };
        _verts[baseIdx + 0] = new SDL_Vertex { position = new SDL_FPoint { x = a.X, y = a.Y }, color = col };
        _verts[baseIdx + 1] = new SDL_Vertex { position = new SDL_FPoint { x = b.X, y = b.Y }, color = col };
        _verts[baseIdx + 2] = new SDL_Vertex { position = new SDL_FPoint { x = c.X, y = c.Y }, color = col };
        _verts[baseIdx + 3] = new SDL_Vertex { position = new SDL_FPoint { x = d.X, y = d.Y }, color = col };

        int i = IndexCount;
        _indices[i + 0] = baseIdx + 0;
        _indices[i + 1] = baseIdx + 1;
        _indices[i + 2] = baseIdx + 2;
        _indices[i + 3] = baseIdx + 0;
        _indices[i + 4] = baseIdx + 2;
        _indices[i + 5] = baseIdx + 3;

        VertexCount += 4;
        IndexCount += 6;
    }

    /// <summary>Adds a thick line from a to b (a quad perpendicular to the segment).</summary>
    public void AddLine(Vector2D a, Vector2D b, float width, Color color)
    {
        Vector2D diff = b - a;
        Vector2D dir = diff.GetNormalized();
        Vector2D perp = new Vector2D(-dir.Y, dir.X) * (width * 0.5f);
        AddQuad(a - perp, a + perp, b + perp, b - perp, color);
    }

    /// <summary>
    /// Adds a polyline as a series of thick line quads (square joins — fast, chunky).
    /// </summary>
    public void AddPolyline(IReadOnlyList<Vector2D> points, float width, Color color)
    {
        for (int i = 0; i < points.Count - 1; i++)
        {
            float len = (points[i + 1] - points[i]).Length;
            if (len <= 0.0001f) continue;
            AddLine(points[i], points[i + 1], width, color);
        }
    }

    // TODO: AddPolygon — triangulate a concave polygon into this mesh (via PolygonTriangulator).
    // public void AddPolygon(IReadOnlyList<Vector2D> points, Color color) { }

    private void EnsureVerts(int needed)
    {
        if (_verts.Length < needed)
            _verts = new SDL_Vertex[Math.Max(needed, _verts.Length * 2)];
    }

    private void EnsureIndices(int needed)
    {
        if (_indices.Length < needed)
            _indices = new int[Math.Max(needed, _indices.Length * 2)];
    }

    /// <summary>Underlying vertex array for submission. Valid until the next <see cref="Clear"/> or add.</summary>
    public SDL_Vertex[] GetVertices() => _verts;

    /// <summary>Underlying index array for submission. Valid until the next <see cref="Clear"/> or add.</summary>
    public int[] GetIndices() => _indices;
}
