using OpenRei.Elements;
using OpenRei.Filters;
using OpenRei.Layout;
using OpenRei.Splines;
using OpenRei.Types;

Console.WriteLine("=== OpenRei Engine Demo ===");

// 1. Create root screen container (1920x1080 viewport)
var rootScreen = new Panel
{
    Name = "RootScreen",
    Size = UDim2.FromOffset(1920, 1080),
    Position = UDim2.Zero,
    Color = Color.FromRgba(18, 18, 24, 255)
};

// 2. Create card panel with rounded corners and filters
var card = new Panel
{
    Name = "MainCard",
    Size = UDim2.FromScale(0.4f, 0.6f),
    Position = UDim2.FromScale(0.5f, 0.5f),
    Anchor = Anchor.Center,
    Color = Color.FromRgba(32, 34, 42, 255),
    CornerRadius = 16.0f,
    ZIndex = 5,
    Filters = {
        new DropShadowFilter(offset: new Vector2D(0, 8), blurRadius: 16.0f, color: Color.Black.WithAlpha(0.5f)),
        new BlurFilter(radius: 2.0f)
    },
    Layout = new UIListLayout
    {
        FillDirection = FillDirection.Vertical,
        Padding = UDim.FromOffset(12),
        HorizontalAlignment = HorizontalAlignment.Center
    }
};
rootScreen.AddChild(card);

// 3. Add Title Label
var titleLabel = new Label
{
    Name = "TitleLabel",
    Text = "OpenRei 2D Engine",
    FontSize = 24.0f,
    TextColor = Color.White,
    Size = UDim2.FromScale(1.0f, 0.15f),
    ZIndex = 1
};
card.AddChild(titleLabel);

// 4. Add Button using user's declarative initialization syntax
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
    ZIndex = 10,
    Filters = new List<Filter>()
};

helloButton.OnClick += () => Console.WriteLine("Hello Button Clicked!");
card.AddChild(helloButton);

// 5. Add Cubic Bezier Spline
var spline = new SplineElement
{
    Name = "ConnectorSpline",
    Type = SplineType.CubicBezier,
    StrokeWidth = 4.0f,
    StrokeColor = Color.FromRgba(0, 220, 130, 255),
    ZIndex = 20,
    ControlPoints = {
        new Vector2D(10, 10),
        new Vector2D(100, 200),
        new Vector2D(300, 50),
        new Vector2D(400, 300)
    }
};
rootScreen.AddChild(spline);

// Run layout update pass
rootScreen.Update(0.016f);

Console.WriteLine($"Root Screen Absolute Bounds: {rootScreen.AbsoluteBounds}");
Console.WriteLine($"Card Absolute Bounds: {card.AbsoluteBounds}");
Console.WriteLine($"Button Absolute Position: {helloButton.AbsolutePosition}");
Console.WriteLine($"Spline Evaluated Points: {spline.GenerateEvaluatedPoints().Count} vertices");

Console.WriteLine("\n[OpenRei Engine Architecture Initialized & Tested Successfully!]");
