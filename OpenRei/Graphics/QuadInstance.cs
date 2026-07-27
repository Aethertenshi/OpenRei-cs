using System.Runtime.InteropServices;
using OpenRei.Types;

namespace OpenRei.Graphics;

/// <summary>
/// Unmanaged 32-byte aligned GPU instance data structure for zero-overhead quad batching.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 16)]
public readonly struct QuadInstance
{
    public readonly Rect Bounds;          // X, Y, Width, Height (16 bytes)
    public readonly Color Color;          // R, G, B, A (16 bytes)
    public readonly float CornerRadius;   // Anti-aliased corner radius (4 bytes)
    public readonly float ZIndex;         // Render depth (4 bytes)
    public readonly ushort TextureId;     // Texture Atlas / RenderTarget index (2 bytes)
    public readonly ushort Flags;         // Pipeline & Masking flags (2 bytes)

    public QuadInstance(Rect bounds, Color color, float cornerRadius = 0.0f, float zIndex = 1.0f, ushort textureId = 0, ushort flags = 0)
    {
        Bounds = bounds;
        Color = color;
        CornerRadius = cornerRadius;
        ZIndex = zIndex;
        TextureId = textureId;
        Flags = flags;
    }
}
