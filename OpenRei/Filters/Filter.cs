namespace OpenRei.Filters;

/// <summary>
/// Abstract base class for injectable CSS-style post-processing filters.
/// </summary>
public abstract class Filter
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Called to apply the post-processing filter effect to an element's render texture.
    /// </summary>
    public abstract void Apply();
}
