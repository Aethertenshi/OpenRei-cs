using reistar.Core;
using reistar.Maths;
using reistar.Shapes;
using reistar.Renderer.SDL3;

// 1. Launch the engine
new MyGame().Run();

// 2. Clean, code-only Game class
public class MyGame : Game
{
    public MyGame() : base(new SdlRenderer("OpenReiStar Sample Game", 1280, 720)) { }

    protected override void OnRender()
    {
        // 1. Draw centered red rectangle (ZIndex = 10)
        Shapes.DrawRect(
            Renderer,
            position: UVect.FromScale(0.5f, 0.5f),
            size: UVect.FromOffset(200, 150),
            color: Color.Red,
            zIndex: 10
        );

        // 2. Draw overlay green box (ZIndex = 20)
        Shapes.DrawRect(
            Renderer,
            position: UVect.FromOffset(100, 100),
            size: UVect.FromOffset(120, 80),
            color: Color.Green,
            zIndex: 20
        );
    }
}
