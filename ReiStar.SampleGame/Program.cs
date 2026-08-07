using System;
using System.IO;
using reistar.Core;
using reistar.Maths;
using reistar.Shapes;
using reistar.Graphics;
using reistar.Renderer.SDL3;
using reistar.Points.UI;

// 1. Launch the engine
new MyGame().Run();

// 2. Clean, code-only Game class
public class MyGame : Game
{
    private Font _myFont = null!;

    public MyGame() : base(new SdlRenderer("OpenReiStar - Text & UI Demo", 1280, 720)) { }

    protected override void OnInitialize()
    {
        var ui = Points.AttachPoint(new UIFeaturePoint());

        string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoogleSans-Regular.ttf");
        _myFont = new Font(fontPath, 32);

        var panel = new Container
        {
            Id = "MainPanel",
            Position = UVect.FromScale(0.5f, 0.5f),
            Size = UVect.FromOffset(360, 440),
            Anchor = Anchor.Center,
            BackgroundColor = new Color(30, 30, 45, 230),
            Layout = LayoutMode.VerticalStack,
            Padding = 1f,
            Spacing = 1f
        };

        panel.AddChild(new Label("REISTAR ENGINE", _myFont, fontSize: 14f, textColor: Color.Yellow));
        panel.AddChild(new Label("1. START GAME", _myFont, fontSize: 20f, textColor: Color.Green));
        panel.AddChild(new Label("2. SETTINGS", _myFont, fontSize: 20f, textColor: Color.White));
        panel.AddChild(new Label("3. EXIT GAME", _myFont, fontSize: 20f, textColor: Color.Red));

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
            _myFont,
            "GoogleSans Font Loaded Successfully!",
            position: UVect.FromScale(0.5f, 0.08f),
            fontSize: 28f,
            color: Color.White,
            anchor: Anchor.Center,
            zIndex: 100
        );
    }
}
