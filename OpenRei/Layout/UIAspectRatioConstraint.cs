using OpenRei.Elements;

namespace OpenRei.Layout;

public enum DominantAxis
{
    Height,
    Width
}

/// <summary>
/// Constrains an element's aspect ratio dynamically based on parent scale and dominant axis.
/// Supports AccountOffset toggle to include or exclude absolute pixel offsets in aspect calculations.
/// </summary>
public class UIAspectRatioConstraint : LayoutModifier
{
    public float AspectRatio { get; set; } = 1.0f;
    public DominantAxis AspectAxis { get; set; } = DominantAxis.Height;
    public bool AccountOffset { get; set; } = true;

    public UIAspectRatioConstraint() { }

    public UIAspectRatioConstraint(float aspectRatio, DominantAxis aspectAxis = DominantAxis.Height, bool accountOffset = true)
    {
        AspectRatio = aspectRatio;
        AspectAxis = aspectAxis;
        AccountOffset = accountOffset;
    }

    public override void UpdateLayout(Element parent)
    {
        // Aspect ratio is evaluated in parent.AbsoluteSize computation
    }
}
