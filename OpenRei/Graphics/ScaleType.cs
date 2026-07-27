namespace OpenRei.Graphics;

/// <summary>
/// Specifies how an image or texture scales to fit its containing element bounds (CSS object-fit & Roblox ScaleType compliant).
/// </summary>
public enum ScaleType
{
    /// <summary>
    /// Stretches texture to completely fill element bounds (CSS object-fit: fill).
    /// </summary>
    Stretch = 0,
    Fill = 0,

    /// <summary>
    /// Scales texture to fit inside element bounds while preserving aspect ratio (CSS object-fit: contain).
    /// </summary>
    Fit = 1,
    Contain = 1,

    /// <summary>
    /// Scales texture to cover element bounds while preserving aspect ratio, cropping overflowing edges (CSS object-fit: cover).
    /// </summary>
    Crop = 2,
    Cover = 2,

    /// <summary>
    /// Displays image at native 1:1 pixel dimensions centered inside bounds (CSS object-fit: none).
    /// </summary>
    None = 3,

    /// <summary>
    /// Repeats texture pattern across element bounds.
    /// </summary>
    Tile = 4
}
