namespace reistar.Points.UI;

using reistar.Core;
using reistar.Graphics;
using reistar.Maths;

public class UIPoint : IPoint
{
    public string Name => "ReiStar.UI";
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

    public void OnUpdate(float deltaTime) { }

    public void OnRender(IRenderer renderer)
    {
        Vect2D canvasSize = renderer.CanvasSize;
        Root.CalculateLayout(canvasSize, Vect2D.Zero, depth: 0);
        Root.Draw(renderer);
    }

    public void OnDetach() { }
}
