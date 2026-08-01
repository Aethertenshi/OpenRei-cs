using OpenRei.Types;

namespace OpenRei.Filters;

/// <summary>
/// A drop shadow post-processing filter.
/// </summary>
public class DropShadowFilter : Filter
{
    public Vector2D Offset { get; set; } = new(0f, 4f);
    public float BlurRadius { get; set; } = 8.0f;
    public Color Color { get; set; } = Color.Black.WithAlpha(0.5f);

    private BlurFilter? _blurCache;

    public DropShadowFilter() { }

    public DropShadowFilter(Vector2D offset, float blurRadius, Color color)
    {
        Offset = offset;
        BlurRadius = blurRadius;
        Color = color;
    }

    /// <summary>Returns a reusable BlurFilter, updating its radius to match BlurRadius. Avoids per-frame allocation.</summary>
    public BlurFilter GetBlurFilter()
    {
        if (_blurCache == null)
        {
            _blurCache = new BlurFilter(BlurRadius);
        }
        else if (Math.Abs(_blurCache.Radius - BlurRadius) > 0.001f)
        {
            _blurCache.Radius = BlurRadius;
            _blurCache.Sigma = BlurRadius * 0.5f;
        }
        return _blurCache;
    }

    public override void Apply()
    {
        if (!Enabled) return;
        // Drop shadow shader pass logic
    }
}
