namespace reistar.Renderer.SDL3;

using System;
using reistar.Core;
using reistar.Graphics;
using reistar.Maths;

public class NullRenderer : IRenderer
{
    private readonly IWindow? _window;

    public IWindow? Window => _window;
    public Vect2D CanvasSize => _window?.Size ?? new Vect2D(1280, 720);

    /// <summary>
    /// Headless NullRenderer with no window attached.
    /// </summary>
    public NullRenderer()
    {
        _window = null;
    }

    /// <summary>
    /// NullRenderer wrapping an existing window instance.
    /// </summary>
    public NullRenderer(IWindow window)
    {
        _window = window;
    }

    /// <summary>
    /// Spawns a native SdlWindow with no GPU rendering device attached.
    /// </summary>
    public NullRenderer(string title, int width = 1280, int height = 720)
        : this(new SdlWindow(title, width, height))
    {
    }

    public void BeginFrame() { }
    public void EndFrame() { }

    public void DrawRect(Vect2D position, Vect2D size, Color color, int zIndex = 0) { }
    public void DrawRectOutline(Vect2D position, Vect2D size, float thickness, Color color, int zIndex = 0) { }
    public void DrawCircle(Vect2D center, float radius, Color color, int zIndex = 0) { }
    public void DrawLine(Vect2D start, Vect2D end, float thickness, Color color, int zIndex = 0) { }
    public void DrawTexture(ITexture texture, Vect2D position, Vect2D size, Color tint, int zIndex = 0) { }

    public void Dispose()
    {
        _window?.Dispose();
    }
}
