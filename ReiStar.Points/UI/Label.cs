namespace reistar.Points.UI;

using reistar.Maths;
using reistar.Graphics;
using reistar.Shapes;

public class Label : UIElement
{
    public string Text { get; set; } = string.Empty;
    public Font? Font { get; set; }
    public float FontSize { get; set; } = 20f;
    public Color TextColor { get; set; } = Color.White;

    public Label()
    {
        Size = UVect.FromOffset(0, 0);
        Layout = LayoutMode.None;
    }

    public Label(string text, Font? font = null, float fontSize = 20f, Color textColor = default)
    {
        Text = text;
        Font = font;
        FontSize = fontSize;
        TextColor = textColor.A == 0 ? Color.White : textColor;
        Size = UVect.FromOffset(0, 0);
        Layout = LayoutMode.None;
    }

    public override void CalculateLayout(Vect2D containerSize, Vect2D containerTopLeft = default, int depth = 0)
    {
        CalculatedDepth = depth;

        Vect2D resolved = Size.Resolve(containerSize);

        // Auto-fit label size to text bounds if not explicitly sized
        if (Font != null && !string.IsNullOrEmpty(Text))
        {
            Vect2D measured = Font.MeasureString(Text, FontSize);
            float w = (Size.ScaleX == 0f && Size.OffsetX == 0f) ? measured.X : resolved.X;
            float h = (Size.ScaleY == 0f && Size.OffsetY == 0f) ? (measured.Y > 0 ? measured.Y : FontSize) : resolved.Y;
            resolved = new Vect2D(w, h);
        }
        else if (Size.ScaleX == 0f && Size.ScaleY == 0f && Size.OffsetX == 0f && Size.OffsetY == 0f)
        {
            resolved = new Vect2D(0f, FontSize);
        }

        ResolvedSize = resolved;

        Vect2D rawPos = Position.Resolve(containerSize);
        ResolvedTopLeft = new Vect2D(
            containerTopLeft.X + rawPos.X - (ResolvedSize.X * Anchor.X),
            containerTopLeft.Y + rawPos.Y - (ResolvedSize.Y * Anchor.Y)
        );

        if (Children.Count > 0)
        {
            Vect2D contentAreaTopLeft = new Vect2D(ResolvedTopLeft.X + Padding, ResolvedTopLeft.Y + Padding);
            Vect2D contentAreaSize = new Vect2D(
                Math.Max(0, ResolvedSize.X - (Padding * 2f)),
                Math.Max(0, ResolvedSize.Y - (Padding * 2f))
            );

            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].CalculateLayout(contentAreaSize, contentAreaTopLeft, depth + 1);
            }
        }
    }

    public override void Draw(IRenderer renderer)
    {
        base.Draw(renderer);

        if (!string.IsNullOrEmpty(Text) && Font != null)
        {
            int effectiveZIndex = (CalculatedDepth * 10) + ZIndex + 1;
            Shapes.DrawText(renderer, Font, Text, ResolvedTopLeft, FontSize, TextColor, Anchor.TopLeft, effectiveZIndex);
        }
    }
}
