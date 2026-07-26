namespace OpenRei.Filters;

/// <summary>
/// A Gaussian blur post-processing filter.
/// </summary>
public class BlurFilter : Filter
{
    public float Radius { get; set; } = 4.0f;

    public BlurFilter() { }

    public BlurFilter(float radius)
    {
        Radius = radius;
    }

    public override void Apply()
    {
        if (!Enabled || Radius <= 0f) return;
        // Blur shader pass logic
    }
}
