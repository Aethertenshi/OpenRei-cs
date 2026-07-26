using OpenRei.Core;
using OpenRei.Elements;
using OpenRei.Filters;
using OpenRei.InputSystem;
using OpenRei.Layout;
using OpenRei.Splines;
using OpenRei.Types;

Console.WriteLine("=== OpenRei Engine App Lifecycle Demo ===");

// 1. Setup event-driven Input listeners
Input.Begin += (key) => Console.WriteLine($"[Input Event] Key Pressed: {key}");
Input.Hold += (key, dt) => Console.WriteLine($"[Input Event] Key Holding: {key} (dt: {dt:F4}s)");
Input.Ended += (key) => Console.WriteLine($"[Input Event] Key Released: {key}");

// 2. Initialize application entry point:
// App.Window(new Vector2D(1920, 1080), "title", WindowFlags)
var app = App.Window(new Vector2D(1920, 1080), "OpenRei Engine Game", WindowFlags.Resizable | WindowFlags.VSync);

// <-- game / app code lives here -->

var mainCard = new Panel
{
    Name = "MainCard",
    Size = UDim2.FromScale(0.4f, 0.6f),
    Position = UDim2.FromScale(0.5f, 0.5f),
    Anchor = Anchor.Center,
    Color = Color.FromRgba(32, 34, 42, 255),
    CornerRadius = 16.0f,
    ZIndex = 5,
    Filters = {
        new DropShadowFilter(offset: new Vector2D(0, 8), blurRadius: 16.0f, color: Color.Black.WithAlpha(0.5f))
    },
    Layout = new UIListLayout
    {
        FillDirection = FillDirection.Vertical,
        Padding = UDim.FromOffset(12),
        HorizontalAlignment = HorizontalAlignment.Center
    }
};

app.RootElement.AddChild(mainCard);

var helloButton = new Button
{
    Name = "HelloButton",
    Size = UDim2.FromOffset(220, 44),
    Position = UDim2.FromScale(0.5f, 0.5f),
    Anchor = Anchor.Center,
    Color = Color.FromRgba(0, 150, 255, 255),
    CornerRadius = 8.0f,
    Text = "hello",
    TextColor = Color.White,
    ZIndex = 10
};

helloButton.OnClick += () => Console.WriteLine("[Event] Hello Button Clicked!");
mainCard.AddChild(helloButton);

// Simulate input triggering
Input.TriggerBegin(KeyType.Space);
Input.TriggerBegin(KeyType.MouseLeft);

// 3. Launch application loop
app.Run();

Input.TriggerEnded(KeyType.Space);
Input.TriggerEnded(KeyType.MouseLeft);
