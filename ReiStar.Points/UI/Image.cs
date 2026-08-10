namespace reistar.Points.UI;

using reistar.Maths;
using reistar.Graphics;

public class Image : UIElement
{
    public ITexture? Texture { get; set; }
    public Color Tint { get; set; } = Color.White;
    public float U0 { get; set; } = 0f;
    public float V0 { get; set; } = 0f;
    public float U1 { get; set; } = 1f;
    public float V1 { get; set; } = 1f;

    public Image()
    {
        Size = UVect.FromOffset(0, 0);
        Layout = LayoutMode.None;
    }

    public Image(ITexture texture, UVect? position = null, UVect? size = null, Color tint = default)
    {
        Texture = texture;
        Position = position ?? UVect.FromOffset(0, 0);
        Size = size ?? UVect.FromOffset(texture.Width, texture.Height);
        Tint = tint.A == 0 ? Color.White : tint;
        Layout = LayoutMode.None;
    }

    public override void CalculateLayout(Vect2D containerSize, Vect2D containerTopLeft = default, int depth = 0)
    {
        CalculatedDepth = depth;

        Vect2D resolved = Size.Resolve(containerSize);

        // Auto-fit image size to texture dimensions if size is unconstrained (0, 0)
        if (Texture != null && Size.ScaleX == 0f && Size.ScaleY == 0f && Size.OffsetX == 0f && Size.OffsetY == 0f)
        {
            resolved = new Vect2D(Texture.Width, Texture.Height);
        }

        ResolvedSize = resolved;

        Vect2D rawPos = Position.Resolve(containerSize);
        ResolvedTopLeft = new Vect2D(
            containerTopLeft.X + rawPos.X - (ResolvedSize.X * Anchor.X),
            containerTopLeft.Y + rawPos.Y - (ResolvedSize.Y * Anchor.Y)
        );
    }

    public override void Draw(IRenderer renderer)
    {
        base.Draw(renderer);

        if (Texture != null)
        {
            int effectiveZIndex = (CalculatedDepth * 10) + ZIndex + 1;
            renderer.DrawTexturedQuad(Texture, ResolvedTopLeft, ResolvedSize, U0, V0, U1, V1, Tint, effectiveZIndex);
        }
    }
}
