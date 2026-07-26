using OpenRei.Elements;
using OpenRei.InputSystem;
using OpenRei.Types;

namespace OpenRei.Core;

/// <summary>
/// Main application engine entry point and window lifecycle manager.
/// Syntax: App.Window(new Vector2D(1920, 1080), "Title", WindowFlags.Resizable)... App.Run();
/// </summary>
public class App
{
    private static App? _instance;
    public static App Instance => _instance ?? throw new InvalidOperationException("App window has not been initialized. Call App.Window(...) first.");

    public Vector2D Size { get; private set; }
    public string Title { get; private set; }
    public WindowFlags Flags { get; private set; }
    public bool IsRunning { get; private set; }

    public Element RootElement { get; set; }

    private App(Vector2D size, string title, WindowFlags flags)
    {
        Size = size;
        Title = title;
        Flags = flags;
        RootElement = new Panel
        {
            Name = "RootViewport",
            Size = UDim2.FromOffset(size.X, size.Y),
            Position = UDim2.Zero
        };
    }

    /// <summary>
    /// Initiates an application window instance.
    /// Example: App.Window(new Vector2D(1920, 1080), "My Game", WindowFlags.Resizable)
    /// </summary>
    public static App Window(Vector2D size, string title, WindowFlags flags = WindowFlags.Default)
    {
        _instance = new App(size, title, flags);
        return _instance;
    }

    /// <summary>
    /// Mounts a root screen/element tree to the application viewport.
    /// </summary>
    public App Mount(Element root)
    {
        RootElement = root;
        return this;
    }

    /// <summary>
    /// Opt-in virtual tick method on App level. Override this in custom App sub-classes.
    /// </summary>
    protected virtual void Tick(float deltaTime)
    {
    }

    /// <summary>
    /// Starts the main application event loop and render pipeline.
    /// </summary>
    public void Run()
    {
        IsRunning = true;
        Console.WriteLine($"[OpenRei App] Launching Window '{Title}' ({Size.X}x{Size.Y}) with Flags: {Flags}");

        float lastTime = 0.0f;

        // Simulated game loop execution
        for (int frame = 0; frame < 3; frame++)
        {
            float currentTime = (frame + 1) * 0.016f;
            float deltaTime = currentTime - lastTime;
            lastTime = currentTime;

            // 1. Process continuous held inputs
            Input.TriggerHold(deltaTime);

            // 2. Opt-in virtual tick execution
            Tick(deltaTime);

            // 3. Scene graph hierarchy update pass
            RootElement.Size = UDim2.FromOffset(Size.X, Size.Y);
            RootElement.Update(deltaTime);

            // 4. Render pass
            RootElement.Render();
        }

        Console.WriteLine("[OpenRei App] Event loop executed cleanly.");
    }
}
