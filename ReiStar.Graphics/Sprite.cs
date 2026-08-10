namespace reistar.Graphics;

using reistar.Maths;

public class Sprite
{
    public ITexture Texture { get; set; }
    public Vect2D Position { get; set; } = Vect2D.Zero;
    public Vect2D Size { get; set; }
    public Color Tint { get; set; } = Color.White;
    public float U0 { get; set; } = 0f;
    public float V0 { get; set; } = 0f;
    public float U1 { get; set; } = 1f;
    public float V1 { get; set; } = 1f;

    public Sprite(ITexture texture)
    {
        Texture = texture;
        Size = new Vect2D(texture.Width, texture.Height);
    }

    public Sprite(ITexture texture, float u0, float v0, float u1, float v1)
    {
        Texture = texture;
        U0 = u0;
        V0 = v0;
        U1 = u1;
        V1 = v1;
        Size = new Vect2D(texture.Width * (u1 - u0), texture.Height * (v1 - v0));
    }

    public void Draw(IRenderer renderer, Vect2D position, int zIndex = 0)
    {
        renderer.DrawTexturedQuad(Texture, position, Size, U0, V0, U1, V1, Tint, zIndex);
    }

    public void Draw(IRenderer renderer, Vect2D position, Vect2D size, int zIndex = 0)
    {
        renderer.DrawTexturedQuad(Texture, position, size, U0, V0, U1, V1, Tint, zIndex);
    }
}
