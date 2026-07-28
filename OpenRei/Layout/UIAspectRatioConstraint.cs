using OpenRei.Elements;

namespace OpenRei.Layout;

public enum DominantAxis
{
    Height,
    Width
}

/// <summary>
/// Constrains an element's aspect ratio dynamically based on parent scale and dominant axis.
/// </summary>
public class UIAspectRatioConstraint : LayoutModifier
{
    public float AspectRatio { get; set; } = 1.0f;
    public DominantAxis AspectAxis { get; set; } = DominantAxis.Height;

    public UIAspectRatioConstraint() { }

    public UIAspectRatioConstraint(float aspectRatio, DominantAxis aspectAxis = DominantAxis.Height)
    {
        AspectRatio = aspectRatio;
        AspectAxis = aspectAxis;
    }

    public override void UpdateLayout(Element parent)
    {
        // Aspect ratio is evaluated in parent.AbsoluteSize computation
    }
}
