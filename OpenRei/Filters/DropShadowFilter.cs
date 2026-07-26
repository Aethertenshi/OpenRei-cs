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

    public DropShadowFilter() { }

    public DropShadowFilter(Vector2D offset, float blurRadius, Color color)
    {
        Offset = offset;
        BlurRadius = blurRadius;
        Color = color;
    }

    public override void Apply()
    {
        if (!Enabled) return;
        // Drop shadow shader pass logic
    }
}
