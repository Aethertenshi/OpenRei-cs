namespace reistar.Points.UI;

/// <summary>
/// Defines how an image is scaled and fitted within its layout bounds, similar to CSS object-fit.
/// </summary>
public enum ImageSizeMode
{
    /// <summary>
    /// Stretches the image to fill the target container bounds completely, ignoring aspect ratio (CSS 'fill' / 'stretch').
    /// </summary>
    Fill,

    /// <summary>
    /// Scales the image to fit entirely within the target container while maintaining aspect ratio (CSS 'contain' / 'fit').
    /// Letterboxing / pillarboxing margins may appear.
    /// </summary>
    Contain,

    /// <summary>
    /// Scales the image to cover the entire target container while maintaining aspect ratio (CSS 'cover').
    /// Excess areas outside the container bounds are cropped via UV mapping.
    /// </summary>
    Cover,

    /// <summary>
    /// Renders the image centered at its native original pixel dimensions without scaling (CSS 'none' / 'center').
    /// </summary>
    None,

    /// <summary>
    /// Crops and renders a specific source sub-region of the texture (CSS 'crop' / 'slice').
    /// </summary>
    Crop
}
