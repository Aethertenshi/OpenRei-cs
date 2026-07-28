using OpenRei.Core;
using OpenRei.Elements;
using OpenRei.Filters;
using OpenRei.Graphics;
using OpenRei.InputSystem;
using OpenRei.IO;
using OpenRei.Layout;
using OpenRei.Rhythm;
using OpenRei.Splines;
using OpenRei.Tween;
using OpenRei.Types;

Console.WriteLine("=== OpenRei Engine SDL3 Window Demo ===");

// 1. Setup event-driven Input & Drag-and-Drop listeners
Input.Begin += (key) => Console.WriteLine($"[Input Event] Key Pressed: {key}");
Input.Ended += (key) => Console.WriteLine($"[Input Event] Key Released: {key}");

FileDropHandler.OnFileDropped += (file) =>
{
    Console.WriteLine($"[Drag-and-Drop Event] File Dropped onto Window: {file}");
    if (OszImporter.IsOszFile(file))
    {
        string? songFolder = OszImporter.Import(file, "Songs");
        Console.WriteLine($"[OszImporter] Song extracted to: {songFolder}");
    }
};

// 2. Initialize application entry point:
// App.Window(new Vector2D(1280, 720), "title", WindowFlags)
var app = App.Window(new Vector2D(1280, 720), "OpenRei 2D Engine", WindowFlags.Borderless | WindowFlags.VSync);

// <-- game / app code lives here -->

var mainCard = new Panel
{
    Name = "MainCard",
    Size = UDim2.FromScale(0.5f, 0.6f),
    Position = UDim2.FromScale(0.5f, 0.5f),
    Anchor = Anchor.Center,
    Color = Color.FromRgba(32, 34, 42, 255),
    CornerRadius = 16.0f,
    ZIndex = 5,
    Filters = {
        new DropShadowFilter(offset: new Vector2D(0, 8), blurRadius: 16.0f, color: Color.Black.WithAlpha(0.5f))
    },
};
app.RootElement.AddChild(mainCard);

var titleLabel = new Label
{
    Name = "TitleLabel",
    Text = "OpenRei Native Window",
    FontSize = 24.0f,
    TextColor = Color.White,
    Size = UDim2.FromScale(1.0f, 0.2f),
    ZIndex = 1
};
mainCard.AddChild(titleLabel);

var helloButton = new Button
{
    Name = "HelloButton",
    Size = UDim2.FromOffset(240, 48),
    Position = UDim2.FromScale(0.5f, 0.5f),
    Anchor = Anchor.Center,
    Color = Color.FromRgba(0, 150, 255, 255),
    CornerRadius = 8.0f,
    Text = "hello",
    TextColor = Color.Black,
    ZIndex = 10
};

helloButton.OnClick += () =>
{
    Console.WriteLine("[Event] Hello Button Clicked! Starting elastic scale tween...");
    var scaleTween = new Tween(1.0f, 1.2f, 0.4f, (val) =>
    {
        helloButton.Size = UDim2.FromOffset(240 * val, 48 * val);
    }, Easing.Elastic, EasingDirection.Out, onComplete: () =>
    {
        var returnTween = new Tween(1.2f, 1.0f, 0.3f, (val) =>
        {
            helloButton.Size = UDim2.FromOffset(240 * val, 48 * val);
        }, Easing.Cubic, EasingDirection.Out);
        returnTween.Start();
    });
    scaleTween.Start();
};
mainCard.AddChild(helloButton);

var scroll = new ScrollingFrame
{
    Size = UDim2.FromScale(.35f, 1.0f),
    Position = UDim2.FromScale(1f, .5f),
    VerticalScrollbarPosition = ScrollbarPlacement.End,
    Anchor = Anchor.CenterRight,
    ScrollSmoothness = 14f,
    PaddingBetween = UDim.FromOffset(20),
    FillDirection = FillDirection.Vertical,
    ZIndex = 6,
    Color = Color.Black
};
for (int i = 0; i < 20; i++)
    scroll.AddChild(new Label { Size = new UDim2(1f, 0, 0, 40), Text = $"Item {i}", Color = Color.Red });
app.RootElement.AddChild(scroll);

Console.WriteLine("Launching native SDL3 desktop window... Press ESC or close window to exit.");

// 3. Launch native SDL3 window loop
app.Run();
