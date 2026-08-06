using OpenRei.Graphics;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// High-performance canvas for rendering large procedural maps (thousands of road
/// segments, parcels, dots) at high FPS.
///
/// TODO: full implementation — build a world-space <see cref="MeshBuilder"/> from map
/// data, transform each vertex by <see cref="Camera"/> (and cull against
/// <see cref="Camera2D.GetWorldViewBounds"/>), then submit a single
/// <see cref="RenderContext.DrawMesh"/> per frame so the whole map renders in one draw call.
/// </summary>
public class MapCanvasElement : Element
{
    /// <summary>World-space camera used to project map geometry.</summary>
    public Camera2D Camera { get; set; } = new();

    /// <summary>Placeholder for the map data model (roads, parcels, dots). TODO: define a typed model.</summary>
    public object? MapData { get; set; }

    private readonly MeshBuilder _mesh = new();

    public MapCanvasElement()
    {
        Name = nameof(MapCanvasElement);
        Color = Color.Transparent;
    }

    public override void Render(RenderContext context)
    {
        if (!Visible) return;

        // TODO: rebuild/transform _mesh from MapData + Camera, then:
        // context.DrawMesh(_mesh, ZIndex);

        base.Render(context);
    }
}
