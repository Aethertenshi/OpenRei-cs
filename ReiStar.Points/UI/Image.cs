namespace reistar.Points.UI;

using System;
using reistar.Maths;
using reistar.Graphics;

public class Image : UIElement
{
    private ITexture? _texture;
    private Color _tint = Color.White;
    private ImageSizeMode _sizeMode = ImageSizeMode.Fill;
    private float _u0 = 0f;
    private float _v0 = 0f;
    private float _u1 = 1f;
    private float _v1 = 1f;

    public ITexture? Texture
    {
        get => _texture;
        set
        {
            if (_texture != value)
            {
                _texture = value;
                MarkDirty();
            }
        }
    }

    public Color Tint
    {
        get => _tint;
        set => _tint = value;
    }

    public ImageSizeMode SizeMode
    {
        get => _sizeMode;
        set
        {
            if (_sizeMode != value)
            {
                _sizeMode = value;
                MarkDirty();
            }
        }
    }

    public float U0
    {
        get => _u0;
        set
        {
            if (_u0 != value)
            {
                _u0 = value;
                MarkDirty();
            }
        }
    }

    public float V0
    {
        get => _v0;
        set
        {
            if (_v0 != value)
            {
                _v0 = value;
                MarkDirty();
            }
        }
    }

    public float U1
    {
        get => _u1;
        set
        {
            if (_u1 != value)
            {
                _u1 = value;
                MarkDirty();
            }
        }
    }

    public float V1
    {
        get => _v1;
        set
        {
            if (_v1 != value)
            {
                _v1 = value;
                MarkDirty();
            }
        }
    }

    public Image()
    {
        Size = UVect.FromOffset(0, 0);
        Layout = LayoutMode.None;
    }

    public Image(ITexture texture, UVect? position = null, UVect? size = null, ImageSizeMode sizeMode = ImageSizeMode.Fill, Color tint = default)
    {
        _texture = texture;
        Position = position ?? UVect.FromOffset(0, 0);
        Size = size ?? UVect.FromOffset(texture.Width, texture.Height);
        _sizeMode = sizeMode;
        _tint = tint.A == 0 ? Color.White : tint;
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

        int effectiveZIndex = (CalculatedDepth * 10) + ZIndex + 2;
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
