using OpenRei.Elements;
using OpenRei.Graphics;
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

    // ── Timers ─────────────────────────────────────────────────────────────────
    private readonly List<(float Remaining, Action Callback)> _timers = new();

    /// <summary>
    /// Schedules <paramref name="callback"/> to fire after <paramref name="seconds"/> have elapsed.
    /// Callbacks run on the main thread during the update loop.
    /// </summary>
    public void Delay(float seconds, Action callback)
    {
        if (callback != null && seconds > 0f)
            _timers.Add((seconds, callback));
    }

    public Vector2D Size { get; private set; }
    public string Title { get; private set; }
    public WindowFlags Flags { get; private set; }
    public bool IsRunning { get; private set; }

    /// <summary>Target frame rate when VSync is off. 0 = unlimited. Default 0.</summary>
    public int TargetFrameRate { get; set; }

    /// <summary>
    /// Current application window width in pixels.
    /// </summary>
    public float WindowWidth => Size.X;

    /// <summary>
    /// Current application window height in pixels.
    /// </summary>
    public float WindowHeight => Size.Y;

    /// <summary>
    /// Primary physical display monitor width in pixels.
    /// </summary>
    public static int ScreenWidth
    {
        get
        {
            unsafe
            {
                SDL_DisplayID displayId = SDL3.SDL_GetPrimaryDisplay();
                SDL_Rect bounds;
                if (SDL3.SDL_GetDisplayBounds(displayId, &bounds))
                {
                    return bounds.w;
                }
            }
            return 1920;
        }
    }

    /// <summary>
    /// Primary physical display monitor height in pixels.
    /// </summary>
    public static int ScreenHeight
    {
        get
        {
            unsafe
            {
                SDL_DisplayID displayId = SDL3.SDL_GetPrimaryDisplay();
                SDL_Rect bounds;
                if (SDL3.SDL_GetDisplayBounds(displayId, &bounds))
                {
                    return bounds.h;
                }
            }
            return 1080;
        }
    }

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
            Position = UDim2.Zero,
            Color = Color.Transparent
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

    /// <summary>Fired every frame. Useful for game logic without subclassing App.</summary>
    public Action<float>? OnTick { get; set; }

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

        // Initialize OpenAL Soft Audio Engine
        OpenRei.Audio.AudioEngine.Initialize();

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

            bool vsync = Flags.HasFlag(WindowFlags.VSync);
            using var graphicsDevice = new GraphicsDevice(window, vsync);
            OpenRei.IO.FileDropHandler.Initialize();

            Console.WriteLine("[OpenRei App] Native SDL3 Window created successfully! Entering main loop...");

            ulong perfFreq = SDL3.SDL_GetPerformanceFrequency();
            ulong lastCounter = SDL3.SDL_GetPerformanceCounter();

            while (IsRunning)
            {
                ulong frameStartCounter = SDL3.SDL_GetPerformanceCounter();
                float deltaTime = MathF.Max((float)((double)(frameStartCounter - lastCounter) / perfFreq), 0.0001f);
                lastCounter = frameStartCounter;

                bool mousePressed = false;
                bool mouseReleased = false;

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
                        KeyType mappedKey = MapKeycode(sdlEvent.key.key);
                        Input.TriggerBegin(mappedKey);
                        if (sdlEvent.key.key == SDL_Keycode.SDLK_ESCAPE)
                        {
                            IsRunning = false;
                        }
                    }
                    else if (eventType == SDL_EventType.SDL_EVENT_KEY_UP)
                    {
                        KeyType mappedKey = MapKeycode(sdlEvent.key.key);
                        Input.TriggerEnded(mappedKey);
                    }
                    else if (eventType == SDL_EventType.SDL_EVENT_MOUSE_MOTION)
                    {
                        Input.MousePosition = new Vector2D(sdlEvent.motion.x, sdlEvent.motion.y);
                    }
                    else if (eventType == SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN)
                    {
                        if (sdlEvent.button.button == 1)
                        {
                            mousePressed = true;
                            Input.TriggerBegin(KeyType.MouseLeft);
                        }
                    }
                    else if (eventType == SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP)
                    {
                        if (sdlEvent.button.button == 1)
                        {
                            mouseReleased = true;
                            Input.TriggerEnded(KeyType.MouseLeft);
                        }
                    }
                    else if (eventType == SDL_EventType.SDL_EVENT_DROP_FILE)
                    {
                        if (sdlEvent.drop.data != null)
                        {
                            string? droppedPath = System.Runtime.InteropServices.Marshal.PtrToStringUTF8((IntPtr)sdlEvent.drop.data);
                            if (!string.IsNullOrEmpty(droppedPath))
                            {
                                OpenRei.IO.FileDropHandler.Enqueue(droppedPath);
                            }
                        }
                    }
                    else if (eventType == SDL_EventType.SDL_EVENT_MOUSE_WHEEL)
                    {
                        Input.TriggerMouseWheel(new Vector2D(sdlEvent.wheel.x, sdlEvent.wheel.y));
                    }
                    else if (eventType == SDL_EventType.SDL_EVENT_WINDOW_RESIZED)
                    {
                        Size = new Vector2D(sdlEvent.window.data1, sdlEvent.window.data2);
                    }
                }

                // Dispatch UI input tree (Hover / Click / Press)
                RootElement.HandleInput(Input.MousePosition, mousePressed, mouseReleased);

                // 1. Process continuous held inputs
                Input.TriggerHold(deltaTime);

                // 2. Resolve pending async audio loads
                OpenRei.Audio.AudioCache.CheckPending();

                // 3. Tick active animation tweens
                OpenRei.Tween.Tween.TickAll(deltaTime);

                // 3b. Tick streaming music track buffers
                OpenRei.Audio.AudioEngine.TickMusicTracks();

                // 4. Fire completed Delay timers
                for (int i = _timers.Count - 1; i >= 0; i--)
                {
                    var t = _timers[i];
                    t.Remaining -= deltaTime;
                    if (t.Remaining <= 0f)
                    {
                        t.Callback();
                        _timers.RemoveAt(i);
                    }
                    else
                    {
                        _timers[i] = t;
                    }
                }

                // 5. Opt-in virtual tick execution + OnTick event
                Tick(deltaTime);
                OnTick?.Invoke(deltaTime);

                // 3. Scene graph hierarchy update pass
                RootElement.Size = UDim2.FromOffset(Size.X, Size.Y);
                RootElement.Update(deltaTime);

                // Reset per-frame input state after update phase
                Input.ResetMouseWheelDelta();

                // 4. Render pass (traversal collects FIFO commands)
                var renderContext = new RenderContext();
                RootElement.Render(renderContext);

                // 5. Hardware GPU Render Pass Execution
                graphicsDevice.RenderPass(null!, renderContext);

                // Frame rate cap (uses high-res performance counter for sub-ms precision)
                if (vsync)
                {
                    SDL3.SDL_Delay(1);
                }
                else if (TargetFrameRate > 0)
                {
                    double targetSec = 1.0 / TargetFrameRate;
                    double elapsedSec = (double)(SDL3.SDL_GetPerformanceCounter() - frameStartCounter) / perfFreq;
                    double remainingSec = targetSec - elapsedSec;
                    if (remainingSec > 0.001)
                    {
                        // Coarse wait via SDL_Delay (yields CPU)
                        SDL3.SDL_Delay(Math.Max(1u, (uint)((remainingSec - 0.001) * 1000)));
                    }
                    // Fine-grained spin-wait for sub-ms precision
                    while ((double)(SDL3.SDL_GetPerformanceCounter() - frameStartCounter) / perfFreq < targetSec)
                        System.Threading.Thread.SpinWait(1);
                }
            }

            OpenRei.IO.FileDropHandler.Shutdown();
            OpenRei.Audio.AudioEngine.Shutdown();
            SDL3.SDL_DestroyWindow(window);
            SDL3.SDL_Quit();
            Console.WriteLine("[OpenRei App] Native window destroyed. Application shut down cleanly.");
        }
    }

    private static KeyType MapKeycode(SDL_Keycode code) => code switch
    {
        SDL_Keycode.SDLK_SPACE => KeyType.Space,
        SDL_Keycode.SDLK_RETURN => KeyType.Enter,
        SDL_Keycode.SDLK_ESCAPE => KeyType.Escape,
        SDL_Keycode.SDLK_TAB => KeyType.Tab,
        SDL_Keycode.SDLK_BACKSPACE => KeyType.Backspace,
        SDL_Keycode.SDLK_UP => KeyType.Up,
        SDL_Keycode.SDLK_DOWN => KeyType.Down,
        SDL_Keycode.SDLK_LEFT => KeyType.Left,
        SDL_Keycode.SDLK_RIGHT => KeyType.Right,
        SDL_Keycode.SDLK_A => KeyType.A,
        SDL_Keycode.SDLK_B => KeyType.B,
        SDL_Keycode.SDLK_C => KeyType.C,
        SDL_Keycode.SDLK_D => KeyType.D,
        SDL_Keycode.SDLK_E => KeyType.E,
        SDL_Keycode.SDLK_F => KeyType.F,
        SDL_Keycode.SDLK_G => KeyType.G,
        SDL_Keycode.SDLK_H => KeyType.H,
        SDL_Keycode.SDLK_I => KeyType.I,
        SDL_Keycode.SDLK_J => KeyType.J,
        SDL_Keycode.SDLK_K => KeyType.K,
        SDL_Keycode.SDLK_L => KeyType.L,
        SDL_Keycode.SDLK_M => KeyType.M,
        SDL_Keycode.SDLK_N => KeyType.N,
        SDL_Keycode.SDLK_O => KeyType.O,
        SDL_Keycode.SDLK_P => KeyType.P,
        SDL_Keycode.SDLK_Q => KeyType.Q,
        SDL_Keycode.SDLK_R => KeyType.R,
        SDL_Keycode.SDLK_S => KeyType.S,
        SDL_Keycode.SDLK_T => KeyType.T,
        SDL_Keycode.SDLK_U => KeyType.U,
        SDL_Keycode.SDLK_V => KeyType.V,
        SDL_Keycode.SDLK_W => KeyType.W,
        SDL_Keycode.SDLK_X => KeyType.X,
        SDL_Keycode.SDLK_Y => KeyType.Y,
        SDL_Keycode.SDLK_Z => KeyType.Z,
        SDL_Keycode.SDLK_0 => KeyType.Num0,
        SDL_Keycode.SDLK_1 => KeyType.Num1,
        SDL_Keycode.SDLK_2 => KeyType.Num2,
        SDL_Keycode.SDLK_3 => KeyType.Num3,
        SDL_Keycode.SDLK_4 => KeyType.Num4,
        SDL_Keycode.SDLK_5 => KeyType.Num5,
        SDL_Keycode.SDLK_6 => KeyType.Num6,
        SDL_Keycode.SDLK_7 => KeyType.Num7,
        SDL_Keycode.SDLK_8 => KeyType.Num8,
        SDL_Keycode.SDLK_9 => KeyType.Num9,
        SDL_Keycode.SDLK_F1 => KeyType.F1,
        SDL_Keycode.SDLK_F2 => KeyType.F2,
        SDL_Keycode.SDLK_F3 => KeyType.F3,
        SDL_Keycode.SDLK_F4 => KeyType.F4,
        SDL_Keycode.SDLK_F5 => KeyType.F5,
        SDL_Keycode.SDLK_F6 => KeyType.F6,
        SDL_Keycode.SDLK_F7 => KeyType.F7,
        SDL_Keycode.SDLK_F8 => KeyType.F8,
        SDL_Keycode.SDLK_F9 => KeyType.F9,
        SDL_Keycode.SDLK_F10 => KeyType.F10,
        SDL_Keycode.SDLK_F11 => KeyType.F11,
        SDL_Keycode.SDLK_F12 => KeyType.F12,
        SDL_Keycode.SDLK_LSHIFT => KeyType.LeftShift,
        SDL_Keycode.SDLK_RSHIFT => KeyType.RightShift,
        SDL_Keycode.SDLK_LCTRL => KeyType.LeftControl,
        SDL_Keycode.SDLK_RCTRL => KeyType.RightControl,
        SDL_Keycode.SDLK_LALT => KeyType.LeftAlt,
        SDL_Keycode.SDLK_RALT => KeyType.RightAlt,
        SDL_Keycode.SDLK_LGUI => KeyType.LeftSuper,
        SDL_Keycode.SDLK_RGUI => KeyType.RightSuper,
        SDL_Keycode.SDLK_CAPSLOCK => KeyType.CapsLock,
        SDL_Keycode.SDLK_INSERT => KeyType.Insert,
        SDL_Keycode.SDLK_DELETE => KeyType.Delete,
        SDL_Keycode.SDLK_HOME => KeyType.Home,
        SDL_Keycode.SDLK_END => KeyType.End,
        SDL_Keycode.SDLK_PAGEUP => KeyType.PageUp,
        SDL_Keycode.SDLK_PAGEDOWN => KeyType.PageDown,
        SDL_Keycode.SDLK_PRINTSCREEN => KeyType.PrintScreen,
        SDL_Keycode.SDLK_PAUSE => KeyType.Pause,
        _ => KeyType.Unknown
    };
}
