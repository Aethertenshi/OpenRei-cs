using OpenRei.Elements;

namespace OpenRei.Layout;

/// <summary>
/// Abstract base class for automatic container layout management (Roblox UIListLayout style).
/// </summary>
public abstract class LayoutModifier
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Recalculates position and size of all child elements inside a parent container.
    /// </summary>
    public abstract void UpdateLayout(Element parent);
}
