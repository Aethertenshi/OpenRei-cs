namespace OpenRei.Graphics;

/// <summary>
/// Specifies how an image or texture scales to fit its containing element bounds.
/// </summary>
public enum ScaleType
{
    /// <summary>
    /// Stretches texture to completely fill element bounds (ignores original aspect ratio).
    /// </summary>
    Stretch,

    /// <summary>
    /// Scales texture to fit inside element bounds while preserving original aspect ratio (letterbox/pillarbox).
    /// </summary>
    Fit,

    /// <summary>
    /// Scales texture to completely fill element bounds while preserving original aspect ratio (crops overflowing edges).
    /// </summary>
    Crop,

    /// <summary>
    /// Repeats texture pattern across element bounds.
    /// </summary>
    Tile
}
