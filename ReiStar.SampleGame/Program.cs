using reistar.Core;
using reistar.Maths;
using reistar.Shapes;
using reistar.Renderer.SDL3;
using reistar.Points.UI;
using reistar.Graphics;

// 1. Launch the engine
new MyGame().Run();

// 2. Clean, code-only Game class
public class MyGame : Game
{
    public MyGame() : base(new SdlRenderer("OpenReiStar - Container UI Demo", 1280, 720)) { }

    private Font myFont = new Font();
    protected override void OnInitialize()
    {
        // Attach Layer 3 UI Point module
        var ui = Points.AttachPoint(new UIPoint());

        // Create an approachable Container component
        var panel = new Container
        {
            Id = "MainPanel",
            Position = UVect.FromScale(0.5f, 0.5f),
            Size = UVect.FromOffset(320, 420),
            Anchor = Anchor.Center,
            BackgroundColor = new Color(30, 30, 45, 230),
            Padding = 20f,
            Spacing = 15f
        };

        // Add child Container elements
        panel.AddChild(new Container
        {
            Id = "RedContainer",
            Size = UVect.FromScale(1f, 0f) + UVect.FromOffset(0, 60),
            BackgroundColor = new Color(220, 50, 50, 255),
        });

        panel.AddChild(new Container
        {
            Id = "GreenContainer",
            Size = UVect.FromScale(1f, 0f) + UVect.FromOffset(0, 60),
            BackgroundColor = new Color(50, 180, 50, 255)
        });

        panel.AddChild(new Container
        {
            Id = "BlueContainer",
            Size = UVect.FromScale(1f, 0f) + UVect.FromOffset(0, 60),
            BackgroundColor = new Color(50, 100, 220, 255)
        });

        ui.Root.AddChild(panel);
    }

    protected override void OnRender()
    {
        Shapes.DrawRect(
            Renderer,
            position: UVect.FromScale(0f, 0f),
            size: UVect.FromScale(1f, 1f),
            color: new Color(15, 15, 25, 255),
            zIndex: 0
        );
        Shapes.DrawText(
            Renderer,
            font: myFont,
            text: "OPEN REISTAR ENGINE",
            position: UVect.FromScale(0.5f, 0.1f),
            fontSize: 36f,
            color: Color.White,
            anchor: Anchor.Center, // Centered alignment
            zIndex: 100
        );
    }
}
