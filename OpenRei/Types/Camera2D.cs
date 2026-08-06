namespace OpenRei.Types;

/// <summary>
/// A 2D world-space camera for panning and zooming. Converts between world coordinates
/// and screen coordinates given the viewport (screen) size. Independent of the UI
/// element layout system — map/terrain elements use it to project world geometry.
/// </summary>
public class Camera2D
{
    /// <summary>World position the camera is centered on (in world units).</summary>
    public Vector2D Position { get; set; } = Vector2D.Zero;

    /// <summary>Zoom factor. 1.0 = 1:1, &gt;1 = zoomed in, &lt;1 = zoomed out.</summary>
    public float Zoom { get; set; } = 1.0f;

    /// <summary>Projects a world position to screen coordinates.</summary>
    public Vector2D WorldToScreen(Vector2D worldPos, Vector2D screenSize)
        => (worldPos - Position) * Zoom + (screenSize * 0.5f);

    /// <summary>Projects a screen coordinate back to world position.</summary>
    public Vector2D ScreenToWorld(Vector2D screenPos, Vector2D screenSize)
        => (screenPos - (screenSize * 0.5f)) / Zoom + Position;

    /// <summary>The world-space rectangle currently visible in the viewport (for culling).</summary>
    public Rect GetWorldViewBounds(Vector2D screenSize)
    {
        Vector2D topLeft = ScreenToWorld(Vector2D.Zero, screenSize);
        Vector2D size = new Vector2D(screenSize.X / Zoom, screenSize.Y / Zoom);
        return new Rect(topLeft.X, topLeft.Y, size.X, size.Y);
    }
}
