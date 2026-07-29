namespace OpenRei.Types;

public enum StrokeAlignment
{
    /// <summary>Stroke is drawn inside the element bounds (default).</summary>
    Inside,
    /// <summary>Stroke is centered on the element border (half in, half out).</summary>
    Center,
    /// <summary>Stroke is drawn outside the element bounds.</summary>
    Outside
}

/// <summary>
/// Defines an outline stroke drawn around an element, supporting thickness, color, and alignment.
/// </summary>
public readonly record struct StrokeInfo(float Thickness, Color Color, StrokeAlignment Alignment = StrokeAlignment.Inside)
{
    public static StrokeInfo None => new(0f, Color.Transparent);
}
