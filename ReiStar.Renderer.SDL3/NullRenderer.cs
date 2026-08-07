namespace reistar.Renderer.SDL3;

using System;
using reistar.Core;
using reistar.Graphics;
using reistar.Maths;

public class NullRenderer : IRenderer
{
    public IWindow Window { get; } = null!;
    public Vect2D CanvasSize { get; set; } = new Vect2D(1280, 720);

    public void BeginFrame() { }
    public void DrawRect(Vect2D position, Vect2D size, Color color, int zIndex = 0) { }
    public void DrawRectOutline(Vect2D position, Vect2D size, float thickness, Color color, int zIndex = 0) { }
    public void DrawCircle(Vect2D center, float radius, Color color, int zIndex = 0) { }
    public void DrawLine(Vect2D start, Vect2D end, float thickness, Color color, int zIndex = 0) { }
    public void DrawTexture(ITexture texture, Vect2D position, Vect2D size, Color tint, int zIndex = 0) { }
    public void DrawTexturedQuad(ITexture? texture, Vect2D position, Vect2D size, float u0, float v0, float u1, float v1, Color tint, int zIndex = 0) { }
    public void DrawText(Font font, string text, Vect2D position, float fontSize, Color color, int zIndex = 0) { }
    public ITexture? CreateTexture(int width, int height, byte[] rgbaPixels) => null;
    public void EndFrame() { }

    public void Dispose() { }
}

