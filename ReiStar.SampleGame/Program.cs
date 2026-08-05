using reistar.Core;
using reistar.Maths;
using reistar.Shapes;
using reistar.Renderer.SDL3;
using reistar.Points.UI;

// 1. Launch the engine
new MyGame().Run();

// 2. Clean, code-only Game class
public class MyGame : Game
{
    public MyGame() : base(new SdlRenderer("OpenReiStar - Layer 3 UI Point Demo", 1280, 720)) { }

    protected override void OnInitialize()
    {
        // Attach Layer 3 UI Point module
        var ui = Points.AttachPoint(new UIPoint());

        // Create an Auto-Layout Vertical Panel centered on screen
        var panel = new UIElement
        {
            Id = "MainPanel",
            Position = UVect.FromScale(0.5f, 0.5f),
            Size = UVect.FromOffset(320, 420),
            Anchor = Anchor.Center,
            BackgroundColor = new Color(30, 30, 45, 230),
            Layout = LayoutMode.VerticalStack,
            Padding = 20f,
            Spacing = 15f
        };

        // Add auto-layout nested children (Red, Green, Blue UI boxes)
        panel.AddChild(new UIElement
        {
            Id = "RedItem",
            Size = UVect.FromScale(1f, 0f) + UVect.FromOffset(0, 60),
            BackgroundColor = new Color(220, 50, 50, 255)
        });

        panel.AddChild(new UIElement
        {
            Id = "GreenItem",
            Size = UVect.FromScale(1f, 0f) + UVect.FromOffset(0, 60),
            BackgroundColor = new Color(50, 180, 50, 255)
        });

        panel.AddChild(new UIElement
        {
            Id = "BlueItem",
            Size = UVect.FromScale(1f, 0f) + UVect.FromOffset(0, 60),
            BackgroundColor = new Color(50, 100, 220, 255)
        });

        ui.Root.AddChild(panel);
    }

    protected override void OnRender()
    {
        // Draw raw background shape (Layer 2)
        Shapes.DrawRect(
            Renderer,
            position: UVect.FromScale(0f, 0f),
            size: UVect.FromScale(1f, 1f),
            color: new Color(15, 15, 25, 255),
            zIndex: 0
        );

        // Note: Points.RenderPoints() automatically draws the Layer 3 UI tree!
    }
}
