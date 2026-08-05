namespace reistar.Graphics;

using System;
using reistar.Maths;

public interface IRenderer : IDisposable
{
    Vect2D CanvasSize { get; }

    void BeginFrame();
    void EndFrame();

    // Primitive Z-Indexed Render Commands
    void DrawRect(Vect2D position, Vect2D size, Color color, int zIndex = 0);
    void DrawRectOutline(Vect2D position, Vect2D size, float thickness, Color color, int zIndex = 0);
    void DrawCircle(Vect2D center, float radius, Color color, int zIndex = 0);
    void DrawLine(Vect2D start, Vect2D end, float thickness, Color color, int zIndex = 0);
    void DrawTexture(ITexture texture, Vect2D position, Vect2D size, Color tint, int zIndex = 0);
}
