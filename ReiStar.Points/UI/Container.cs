namespace reistar.Points.UI;

using reistar.Maths;

public class Container : UIElement
{
    public Container()
    {
        Layout = LayoutMode.None;
    }

    public Container(UVect position, UVect size, Color backgroundColor = default)
    {
        Position = position;
        Size = size;
        BackgroundColor = backgroundColor;
        Layout = LayoutMode.None;
    }
}
