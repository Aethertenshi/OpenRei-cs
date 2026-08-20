namespace reistar.Graphics;

using System.Runtime.InteropServices;
using reistar.Maths;

[StructLayout(LayoutKind.Sequential)]
public struct Vertex2D
{
    public float X;
    public float Y;
    public float R;
    public float G;
    public float B;
    public float A;
    public float U;
    public float V;

    public Vertex2D(float x, float y, float r, float g, float b, float a, float u = 0f, float v = 0f)
    {
        X = x;
        Y = y;
        R = r;
        G = g;
        B = b;
        A = a;
        U = u;
        V = v;
    }

    public Vertex2D(Vect2D position, Color color, Vect2D texCoord = default)
    {
        X = position.X;
        Y = position.Y;
        R = color.R / 255f;
        G = color.G / 255f;
        B = color.B / 255f;
        A = color.A / 255f;
        U = texCoord.X;
        V = texCoord.Y;
    }
}
