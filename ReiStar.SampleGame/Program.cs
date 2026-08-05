using reistar.Core;
using reistar.Maths;
using reistar.Shapes;
using reistar.Renderer.SDL3;

// 1. Launch the engine
new MyGame().Run();

// 2. Clean, code-only Game class
public class MyGame : Game
{
    public MyGame() : base(new SdlRenderer("OpenReiStar - Anchor Demo", 1280, 720)) { }

    protected override void OnRender()
    {
        // 1. Draw centered red box using Anchor.Center
        Shapes.DrawRect(
            Renderer,
            position: UVect.FromScale(0.5f, 0.5f), // 50% screen X/Y
            size: UVect.FromOffset(200, 150),
            color: Color.Red,
            anchor: Anchor.Center, // Perfectly centered!
            zIndex: 10
        );

        // 2. Draw top-left blue box using Anchor.TopLeft (default)
        Shapes.DrawRect(
            Renderer,
            position: UVect.FromOffset(20, 20),
            size: UVect.FromOffset(150, 100),
            color: Color.Blue,
            anchor: Anchor.TopLeft,
            zIndex: 15
        );

        // 3. Draw bottom-right green box using Anchor.BottomRight
        Shapes.DrawRect(
            Renderer,
            position: UVect.FromScale(1f, 1f), // 100% screen X/Y
            size: UVect.FromOffset(180, 120),
            color: Color.Green,
            anchor: Anchor.BottomRight, // Anchored to bottom right corner!
            zIndex: 20
        );
    }
}
