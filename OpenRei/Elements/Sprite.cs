using OpenRei.Graphics;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// A high-performance image rendering UI element supporting asynchronous background loading, UV atlas cropping, and aspect-ratio scaling modes.
/// </summary>
public class Sprite : Element
{
    private string? _texturePath;

    public Texture? Texture { get; set; }
    public ScaleType ScaleType { get; set; } = ScaleType.Stretch;
    public Rect? SourceRect { get; set; }
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
        Color = Color.Transparent;
    }

    public override void Render(RenderContext context)
    {
        if (!Visible) return;

        // Render background quad if color is non-transparent
        base.Render(context);

        if (Texture == null || !Texture.IsValid) return;

        Rect bounds = AbsoluteBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        Rect finalBounds = bounds;

        if (ScaleType == ScaleType.Fit)
        {
            float texAspect = (float)Texture.Width / Texture.Height;
            float boundsAspect = bounds.Width / bounds.Height;

            if (boundsAspect > texAspect)
            {
                float fitWidth = bounds.Height * texAspect;
                float fitX = bounds.X + (bounds.Width - fitWidth) * 0.5f;
                finalBounds = new Rect(fitX, bounds.Y, fitWidth, bounds.Height);
            }
            else
            {
                float fitHeight = bounds.Width / texAspect;
                float fitY = bounds.Y + (bounds.Height - fitHeight) * 0.5f;
                finalBounds = new Rect(bounds.X, fitY, bounds.Width, fitHeight);
            }
        }

        context.DrawImage(Texture, finalBounds, SourceRect, ImageColor);
    }
}
