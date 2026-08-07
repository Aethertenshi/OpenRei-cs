namespace reistar.Graphics;

using System;
using reistar.Maths;

public interface IRenderer : IDisposable
{
    Vect2D CanvasSize { get; }
    void BeginFrame();
    void DrawRect(Vect2D position, Vect2D size, Color color, int zIndex = 0);
    void DrawRectOutline(Vect2D position, Vect2D size, float thickness, Color color, int zIndex = 0);
    void DrawCircle(Vect2D center, float radius, Color color, int zIndex = 0);
    void DrawLine(Vect2D start, Vect2D end, float thickness, Color color, int zIndex = 0);
    void DrawTexture(ITexture texture, Vect2D position, Vect2D size, Color tint, int zIndex = 0);
    void DrawTexturedQuad(ITexture? texture, Vect2D position, Vect2D size, float u0, float v0, float u1, float v1, Color tint, int zIndex = 0);
    void DrawText(Font font, string text, Vect2D position, float fontSize, Color color, int zIndex = 0);
    ITexture? CreateTexture(int width, int height, byte[] rgbaPixels);
    void EndFrame();
}


