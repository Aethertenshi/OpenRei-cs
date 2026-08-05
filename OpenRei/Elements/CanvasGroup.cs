namespace OpenRei.Elements;

/// <summary>
/// A UI container element that controls group transparency for itself and all descendant children.
/// Designed for easy modal/panel fade transitions and group tweening (like Roblox CanvasGroup).
/// </summary>
public class CanvasGroup : Panel
{
    public CanvasGroup()
    {
        Name = nameof(CanvasGroup);
    }
}
