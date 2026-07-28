using OpenRei.Types;

namespace OpenRei.Filters;

public class FrostedGlassFilter : Filter
{
    public float Radius { get; set; } = 16f;
    public Color TintColor { get; set; } = Color.White.WithAlpha(0.3f);
    public float Brightness { get; set; } = 1.2f;
    public int Downscale { get; set; } = 2;
    public int Passes { get; set; } = 2;

    public FrostedGlassFilter() { }

    public FrostedGlassFilter(float radius, Color? tint = null, float brightness = 1.2f)
    {
        Radius = radius;
        if (tint.HasValue) TintColor = tint.Value;
        Brightness = brightness;
    }

    public override void Apply() { }
}
