namespace OpenRei.Filters;

/// <summary>
/// A high-performance separable multi-pass Gaussian / Kawase blur post-processing filter inspired by osu!-framework.
/// </summary>
public class BlurFilter : Filter
{
    public float Radius { get; set; } = 16.0f;
    public float Sigma { get; set; } = 8.0f;
    public int Downscale { get; set; } = 2;
    public int Passes { get; set; } = 2;

    public BlurFilter() { }

    public BlurFilter(float radius, int passes = 2, int downscale = 2)
    {
        Radius = radius;
        Sigma = radius * 0.5f;
        Passes = Math.Clamp(passes, 1, 4);
        Downscale = Math.Clamp(downscale, 1, 4);
    }

    public override void Apply() { }
}
