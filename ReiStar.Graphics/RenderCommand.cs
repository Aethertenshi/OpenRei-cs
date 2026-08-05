namespace reistar.Graphics;

using System.Runtime.InteropServices;
using reistar.Maths;

public enum RenderPrimitiveType : byte
{
    Rectangle,
    RectangleOutline,
    Circle,
    Line,
    Texture
}

[StructLayout(LayoutKind.Sequential)]
public struct RenderCommand
{
    public ulong SortKey; // (ZIndex << 32) | SubmissionID
    public RenderPrimitiveType Type;
    public Vect2D Position;
    public Vect2D Size;
    public Color Color;
    public float Thickness;
    public ushort TextureId;
}
