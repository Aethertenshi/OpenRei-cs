namespace reistar.Graphics;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct Vertex2D
{
    public float X;
    public float Y;
    public float R;
    public float G;
    public float B;
    public float A;

    public Vertex2D(float x, float y, float r, float g, float b, float a)
    {
        X = x;
        Y = y;
        R = r;
        G = g;
        B = b;
        A = a;
    }
}
