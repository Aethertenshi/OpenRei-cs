namespace reistar.Points.UI;

using System;
using reistar.Maths;
using reistar.Graphics;

public class Image : UIElement
{
    public ITexture? Texture { get; set; }
    public Color Tint { get; set; } = Color.White;
    public ImageSizeMode SizeMode { get; set; } = ImageSizeMode.Fill;

    public float U0 { get; set; } = 0f;
    public float V0 { get; set; } = 0f;
    public float U1 { get; set; } = 1f;
    public float V1 { get; set; } = 1f;

    public Image()
    {
        Size = UVect.FromOffset(0, 0);
        Layout = LayoutMode.None;
    }

    public Image(ITexture texture, UVect? position = null, UVect? size = null, ImageSizeMode sizeMode = ImageSizeMode.Fill, Color tint = default)
    {
        Texture = texture;
        Position = position ?? UVect.FromOffset(0, 0);
        Size = size ?? UVect.FromOffset(texture.Width, texture.Height);
        SizeMode = sizeMode;
        Tint = tint.A == 0 ? Color.White : tint;
        Layout = LayoutMode.None;
    }

    public override void CalculateLayout(Vect2D containerSize, Vect2D containerTopLeft = default, int depth = 0)
    {
        CalculatedDepth = depth;

        Vect2D resolved = Size.Resolve(containerSize);

        // Auto-fit image size to native texture dimensions if size is unconstrained (0, 0)
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

        if (Texture == null || Texture.Width <= 0 || Texture.Height <= 0) return;

        int effectiveZIndex = (CalculatedDepth * 10) + ZIndex + 1;
        float texW = Texture.Width * MathF.Abs(U1 - U0);
        float texH = Texture.Height * MathF.Abs(V1 - V0);

        Vect2D drawPos = ResolvedTopLeft;
        Vect2D drawSize = ResolvedSize;
        float renderU0 = U0;
        float renderV0 = V0;
        float renderU1 = U1;
        float renderV1 = V1;

        switch (SizeMode)
        {
            case ImageSizeMode.Contain:
                {
                    float scale = MathF.Min(ResolvedSize.X / texW, ResolvedSize.Y / texH);
                    drawSize = new Vect2D(texW * scale, texH * scale);
                    drawPos = new Vect2D(
                        ResolvedTopLeft.X + (ResolvedSize.X - drawSize.X) * 0.5f,
                        ResolvedTopLeft.Y + (ResolvedSize.Y - drawSize.Y) * 0.5f
                    );
                }
                break;

            case ImageSizeMode.Cover:
                {
                    float scale = MathF.Max(ResolvedSize.X / texW, ResolvedSize.Y / texH);
                    float visW = ResolvedSize.X / scale;
                    float visH = ResolvedSize.Y / scale;

                    float uSpan = U1 - U0;
                    float vSpan = V1 - V0;

                    float uMargin = ((texW - visW) / (2f * texW)) * uSpan;
                    float vMargin = ((texH - visH) / (2f * texH)) * vSpan;

                    renderU0 = U0 + uMargin;
                    renderV0 = V0 + vMargin;
                    renderU1 = U1 - uMargin;
                    renderV1 = V1 - vMargin;
                }
                break;

            case ImageSizeMode.None:
                {
                    drawSize = new Vect2D(texW, texH);
                    drawPos = new Vect2D(
                        ResolvedTopLeft.X + (ResolvedSize.X - drawSize.X) * 0.5f,
                        ResolvedTopLeft.Y + (ResolvedSize.Y - drawSize.Y) * 0.5f
                    );
                }
                break;

            case ImageSizeMode.Fill:
            case ImageSizeMode.Crop:
            default:
                break;
        }

        renderer.DrawTexturedQuad(Texture, drawPos, drawSize, renderU0, renderV0, renderU1, renderV1, Tint, effectiveZIndex);
    }
}
