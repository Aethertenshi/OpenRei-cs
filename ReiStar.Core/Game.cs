namespace reistar.Core;

using System.Diagnostics;
using reistar.Graphics;
using reistar.Maths;

public abstract class Game : IDisposable
{
    public IWindow Window { get; }
    public IRenderer Renderer { get; }
    public PointManager Points { get; } = new();
    public EngineContext Context { get; }

    /// <summary>
    /// Explicit constructor for manual window and renderer control.
    /// </summary>
    public Game(IWindow window, IRenderer renderer)
    {
        Window = window;
        Renderer = renderer;
        Context = new EngineContext(Window, Renderer);
        Points.Initialize(Context);
    }

    /// <summary>
    /// Convenience constructor when using a renderer backend that manages its own window.
    /// </summary>
    public Game(IRenderer renderer)
        : this(ExtractWindow(renderer), renderer)
    {
    }

    private static IWindow ExtractWindow(IRenderer renderer)
    {
        // Reflection-free window extraction if renderer provides a Window property
        var windowProp = renderer.GetType().GetProperty("Window");
        if (windowProp?.GetValue(renderer) is IWindow window)
        {
            return window;
        }
        throw new ArgumentException("The provided IRenderer does not expose a Window property. Use Game(IWindow, IRenderer) constructor instead.");
    }

    public void Run()
    {
        OnInitialize();

        Stopwatch stopwatch = Stopwatch.StartNew();
        float lastTime = 0f;

        while (Window.IsRunning)
        {
            float currentTime = (float)stopwatch.Elapsed.TotalSeconds;
            float deltaTime = currentTime - lastTime;
            lastTime = currentTime;

            Window.PollEvents();

            OnUpdate(deltaTime);
            Points.UpdatePoints(deltaTime);

            Renderer.BeginFrame();
            OnRender();
            Points.RenderPoints();
            Renderer.EndFrame();
        }

        OnShutdown();
    }

    protected virtual void OnInitialize() { }
    protected virtual void OnUpdate(float deltaTime) { }
    protected virtual void OnRender() { }
    protected virtual void OnShutdown() { }

    public virtual void Dispose()
    {
        Renderer.Dispose();
        if (Renderer is not IDisposable)
        {
            Window.Dispose();
        }
    }
}
