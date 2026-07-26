using OpenRei.Elements;
using OpenRei.InputSystem;
using OpenRei.Types;
using SDL;

namespace OpenRei.Core;

/// <summary>
/// Main application engine entry point and native SDL3 window lifecycle manager.
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

    protected virtual void Tick(float deltaTime)
    {
    }

    /// <summary>
    /// Starts the main SDL3 window event loop and render pipeline.
    /// </summary>
    public void Run()
    {
        IsRunning = true;
        Console.WriteLine($"[OpenRei App] Initializing SDL3 Window '{Title}' ({Size.X}x{Size.Y})...");

        // Initialize SDL3 Video & Gamepad subsystems
        if (!SDL3.SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_GAMEPAD))
        {
            Console.WriteLine($"[SDL3 Error] Failed to initialize SDL3: {SDL3.SDL_GetError()}");
            return;
        }

        SDL_WindowFlags sdlWindowFlags = SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY;
        if (Flags.HasFlag(WindowFlags.Resizable)) sdlWindowFlags |= SDL_WindowFlags.SDL_WINDOW_RESIZABLE;
        if (Flags.HasFlag(WindowFlags.Fullscreen)) sdlWindowFlags |= SDL_WindowFlags.SDL_WINDOW_FULLSCREEN;
        if (Flags.HasFlag(WindowFlags.Borderless)) sdlWindowFlags |= SDL_WindowFlags.SDL_WINDOW_BORDERLESS;

        unsafe
        {
            var window = SDL3.SDL_CreateWindow(Title, (int)Size.X, (int)Size.Y, sdlWindowFlags);
            if (window == null)
            {
                Console.WriteLine($"[SDL3 Error] Failed to create window: {SDL3.SDL_GetError()}");
                SDL3.SDL_Quit();
                return;
            }

            Console.WriteLine("[OpenRei App] Native SDL3 Window created successfully! Entering main loop...");

            ulong lastTicks = SDL3.SDL_GetTicks();

            while (IsRunning)
            {
                // Calculate Delta Time in seconds
                ulong currentTicks = SDL3.SDL_GetTicks();
                float deltaTime = MathF.Max((currentTicks - lastTicks) / 1000.0f, 0.0001f);
                lastTicks = currentTicks;

                // Process Native SDL3 OS Event Queue
                SDL_Event sdlEvent;
                while (SDL3.SDL_PollEvent(&sdlEvent))
                {
                    var eventType = (SDL_EventType)sdlEvent.type;
                    if (eventType == SDL_EventType.SDL_EVENT_QUIT)
                    {
                        IsRunning = false;
                    }
                    else if (eventType == SDL_EventType.SDL_EVENT_KEY_DOWN)
                    {
                        Input.TriggerBegin(KeyType.Space);
                        if (sdlEvent.key.key == SDL_Keycode.SDLK_ESCAPE)
                        {
                            IsRunning = false;
                        }
                    }
                    else if (eventType == SDL_EventType.SDL_EVENT_KEY_UP)
                    {
                        Input.TriggerEnded(KeyType.Space);
                    }
                    else if (eventType == SDL_EventType.SDL_EVENT_MOUSE_MOTION)
                    {
                        Input.MousePosition = new Vector2D(sdlEvent.motion.x, sdlEvent.motion.y);
                    }
                    else if (eventType == SDL_EventType.SDL_EVENT_WINDOW_RESIZED)
                    {
                        Size = new Vector2D(sdlEvent.window.data1, sdlEvent.window.data2);
                    }
                }

                // 1. Process continuous held inputs
                Input.TriggerHold(deltaTime);

                // 2. Opt-in virtual tick execution
                Tick(deltaTime);

                // 3. Scene graph hierarchy update pass
                RootElement.Size = UDim2.FromOffset(Size.X, Size.Y);
                RootElement.Update(deltaTime);

                // 4. Render pass
                RootElement.Render();

                // Cap frame rate slightly for smooth CPU loop if GPU vsync is inactive
                SDL3.SDL_Delay(1);
            }

            SDL3.SDL_DestroyWindow(window);
            SDL3.SDL_Quit();
            Console.WriteLine("[OpenRei App] Native window destroyed. Application shut down cleanly.");
        }
    }
}
