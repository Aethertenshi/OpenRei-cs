namespace reistar.Points.UI;

using reistar.Core;
using reistar.Graphics;
using reistar.Maths;
using reistar.Input;

public class UIFeaturePoint : IPoint
{
    private Vect2D _lastCanvasSize = Vect2D.Zero;

    public string Name => "ReiStar.UIFeaturePoint";
    public bool Enabled { get; set; } = true;

    public Container Root { get; } = new Container
    {
        Id = "Root",
        Position = UVect.FromScale(0f, 0f),
        Size = UVect.FromScale(1f, 1f),
        Anchor = Anchor.TopLeft,
        BackgroundColor = Color.Transparent
    };

    public void OnAttach(EngineContext context) { }

    public void OnUpdate(float deltaTime)
    {
        Root.Update(deltaTime);

        Vect2D mousePos = Input.MousePosition;
        Root.ProcessMouseMove(mousePos);

        if (Input.IsMouseButtonClicked(MouseButton.Left))
        {
            Root.ProcessMouseDown(mousePos, MouseButton.Left);
        }
        else if (Input.IsMouseButtonReleased(MouseButton.Left))
        {
            Root.ProcessMouseUp(mousePos, MouseButton.Left);
        }

        if (Input.IsMouseButtonClicked(MouseButton.Right))
        {
            Root.ProcessMouseDown(mousePos, MouseButton.Right);
        }
        else if (Input.IsMouseButtonReleased(MouseButton.Right))
        {
            Root.ProcessMouseUp(mousePos, MouseButton.Right);
        }
    }

    public void OnRender(IRenderer renderer)
    {
        Vect2D canvasSize = renderer.CanvasSize;
        if (canvasSize.X != _lastCanvasSize.X || canvasSize.Y != _lastCanvasSize.Y || Root.IsDirty)
        {
            Root.CalculateLayout(canvasSize, Vect2D.Zero, depth: 0);
            Root.ClearDirty();
            _lastCanvasSize = canvasSize;
        }

        Root.Draw(renderer);
    }

    public void OnDetach() { }
}
