namespace reistar.Renderer.SDL3;

using System;
using reistar.Core;
using reistar.Graphics;
using reistar.Maths;

public class NullRenderer : IRenderer, IWindowProvider
{
    public IWindow Window { get; } = new NullWindow();
    public Vect2D CanvasSize => Window.Size;
    public bool VSync { get; set; }

    public void BeginFrame() { }
    public void DrawRect(Vect2D position, Vect2D size, Color color, int zIndex = 0) { }
    public void DrawRectOutline(Vect2D position, Vect2D size, float thickness, Color color, int zIndex = 0) { }
    public void DrawCircle(Vect2D center, float radius, Color color, int zIndex = 0) { }
    public void DrawLine(Vect2D start, Vect2D end, float thickness, Color color, int zIndex = 0) { }
    public void DrawTexture(ITexture texture, Vect2D position, Vect2D size, Color tint, int zIndex = 0) { }
    public void DrawTexturedQuad(ITexture? texture, Vect2D position, Vect2D size, float u0, float v0, float u1, float v1, Color tint, int zIndex = 0) { }
    public void DrawRectRotated(Vect2D position, Vect2D size, float angleDegrees, Vect2D pivot = default, Color color = default, int zIndex = 0) { }
    public void DrawTextureRotated(ITexture texture, Vect2D position, Vect2D size, float angleDegrees, Vect2D pivot = default, Color tint = default, int zIndex = 0) { }
    public void DrawGeometry(ITexture? texture, ReadOnlySpan<Vertex2D> vertices, ReadOnlySpan<int> indices, int zIndex = 0) { }
    public void DrawBackdropBlur(Vect2D position, Vect2D size, float blurAmount = 1.0f, int zIndex = 0) { }
    public void DrawText(Font font, string text, Vect2D position, float fontSize, Color color, int zIndex = 0) { }
    public ITexture? CreateTexture(int width, int height, byte[] rgbaPixels) => null;
    public ITexture? CreateRenderTarget(int width, int height) => null;
    public void SetRenderTarget(ITexture? target) { }
    public ITexture? LoadTexture(string filePath) => null;
    public void EndFrame() { }

    public void Dispose() { }
}

public class NullWindow : IWindow
{
    public string Title { get; set; } = "NullWindow";
    public Vect2D Size => new(1280, 720);
    public bool IsRunning => false;

    public void PollEvents() { }
    public ulong GetPerformanceCounter() => 0;
    public ulong GetPerformanceFrequency() => 10000000;
    public void Dispose() { }
}
