using OpenRei.Filters;
using OpenRei.Graphics;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// A high-performance image rendering UI element supporting asynchronous background loading, UV atlas cropping, color tinting, and CSS object-fit StretchMode scaling.
/// </summary>
public class Sprite : Element
{
    private string? _texturePath;

    public Texture? Texture { get; set; }
    public ScaleType ScaleType { get; set; } = ScaleType.Stretch;

    /// <summary>
    /// Alias property for ScaleType (CSS object-fit compliant: Fill, Contain, Cover, None, Tile).
    /// </summary>
    public ScaleType StretchMode
    {
        get => ScaleType;
        set => ScaleType = value;
    }

    public Rect? SourceRect { get; set; }

    /// <summary>
    /// Explicit image tint color overlay.
    /// </summary>
    public Color ImageColor { get; set; } = Color.White;

    public string? TexturePath
    {
        get => _texturePath;
        set
        {
            if (_texturePath != value)
            {
                _texturePath = value;
                if (!string.IsNullOrEmpty(_texturePath))
                {
                    _ = TextureEngine.LoadAsync(_texturePath).ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully && t.Result != null)
                        {
                            Texture = t.Result;
                        }
                    });
                }
            }
        }
    }

    public Sprite()
    {
        Name = nameof(Sprite);
        Color = Color.White;
    }

    public override void Render(RenderContext context)
    {
        if (!Visible) return;

        // Determine active texture color tint (supports both sprite.Color and sprite.ImageColor)
        Color activeTint = ImageColor != Color.White ? ImageColor : Color;

        // 1. Render this Sprite's texture FIRST with color tint modulation
        if (Texture != null && Texture.IsValid)
        {
            Rect bounds = AbsoluteBounds;
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                Rect destBounds = bounds;
                Rect? activeSourceRect = SourceRect;

                float texW = Texture.Width;
                float texH = Texture.Height;

                if (texW > 0 && texH > 0)
                {
                    switch (ScaleType)
                    {
                        case ScaleType.Fit: // Contain
                            {
                                float texAspect = texW / texH;
                                float boundsAspect = bounds.Width / bounds.Height;

                                if (boundsAspect > texAspect)
                                {
                                    float fitWidth = bounds.Height * texAspect;
                                    float fitX = bounds.X + (bounds.Width - fitWidth) * 0.5f;
                                    destBounds = new Rect(fitX, bounds.Y, fitWidth, bounds.Height);
                                }
                                else
                                {
                                    float fitHeight = bounds.Width / texAspect;
                                    float fitY = bounds.Y + (bounds.Height - fitHeight) * 0.5f;
                                    destBounds = new Rect(bounds.X, fitY, bounds.Width, fitHeight);
                                }
                                break;
                            }

                        case ScaleType.Crop: // Cover
                            {
                                float texAspect = texW / texH;
                                float boundsAspect = bounds.Width / bounds.Height;

                                if (boundsAspect > texAspect)
                                {
                                    float cropH = texW / boundsAspect;
                                    float cropY = (texH - cropH) * 0.5f;
                                    activeSourceRect = new Rect(0, cropY, texW, cropH);
                                }
                                else
                                {
                                    float cropW = texH * boundsAspect;
                                    float cropX = (texW - cropW) * 0.5f;
                                    activeSourceRect = new Rect(cropX, 0, cropW, texH);
                                }
                                break;
                            }

                        case ScaleType.None: // Original 1:1 pixel size centered
                            {
                                float posX = bounds.X + (bounds.Width - texW) * 0.5f;
                                float posY = bounds.Y + (bounds.Height - texH) * 0.5f;
                                destBounds = new Rect(posX, posY, texW, texH);
                                break;
                            }

                        case ScaleType.Tile: // Repeat texture pattern across bounds
                            {
                                for (float y = bounds.Y; y < bounds.Y + bounds.Height; y += texH)
                                {
                                    for (float x = bounds.X; x < bounds.X + bounds.Width; x += texW)
                                    {
                                        float tileW = MathF.Min(texW, bounds.X + bounds.Width - x);
                                        float tileH = MathF.Min(texH, bounds.Y + bounds.Height - y);

                                        Rect tileDest = new Rect(x, y, tileW, tileH);
                                        Rect tileSrc = new Rect(0, 0, tileW, tileH);

                                        context.DrawImage(Texture, tileDest, tileSrc, activeTint, ZIndex);
                                    }
                                }
                                break;
                            }

                        case ScaleType.Stretch: // Fill (Default)
                        default:
                            break;
                    }

                    if (ScaleType != ScaleType.Tile)
                    {
                        context.DrawImage(Texture, destBounds, activeSourceRect, activeTint, ZIndex);
                    }
                }
            }
        }

        // 2. Submit element filters
        foreach (var filter in Filters)
        {
            if (filter is BlurFilter blurFilter && blurFilter.Enabled)
            {
                context.ApplyBlur(AbsoluteBounds, blurFilter);
            }
        }

        // 3. Render child elements on top of this sprite's image
        var sortedChildren = GetSortedChildren();
        foreach (var child in sortedChildren)
        {
            child.Render(context);
        }
    }
}
