using OpenRei.Types;

namespace OpenRei.Elements;

public enum TextAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// A text rendering UI element.
/// </summary>
public class Label : Element
{
    public string Text { get; set; } = string.Empty;
    public Color TextColor { get; set; } = Color.White;
    public float FontSize { get; set; } = 16.0f;
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;

    public Label()
    {
        Name = nameof(Label);
        Color = Color.Transparent;
    }
}
