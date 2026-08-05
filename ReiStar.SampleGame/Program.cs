using reistar.Core;
using reistar.Maths;
using reistar.Shapes;
using reistar.Renderer.SDL3;

// 1. One line to launch the game
new MyGame().Run();

// 2. Clean, minimal Game class
public class MyGame : Game
{
    public MyGame() : base(new SdlGpuRenderer("OpenReiStar Sample Game", 1280, 720)) { }

    protected override void OnInitialize()
    {
        base.OnInitialize();
    }

    protected override void OnRender()
    {
        // Draw background canvas (ZIndex = 0)
        Shapes.DrawRect(
            Renderer,
            position: UVect.FromScale(0f, 0f),
            size: UVect.FromScale(1f, 1f),
            color: new Color(25, 25, 40, 255),
            zIndex: 0
        );

        // Draw centered red rectangle (ZIndex = 10)
        Shapes.DrawRect(
            Renderer,
            position: UVect.FromScale(0.5f, 0.5f),
            size: UVect.FromOffset(200, 150),
            color: Color.Red,
            zIndex: 10
        );
    }
}
