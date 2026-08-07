namespace reistar.Core;

using System;
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
        if (renderer is IWindowProvider windowProvider)
        {
            return windowProvider.Window;
        }
        throw new ArgumentException("The provided IRenderer does not implement IWindowProvider. Use Game(IWindow, IRenderer) constructor instead.");
    }


    public void Run()
    {
        OnInitialize();

        Stopwatch stopwatch = Stopwatch.StartNew();
        float lastTime = (float)stopwatch.Elapsed.TotalSeconds;

        while (Window.IsRunning)
        {
            float currentTime = (float)stopwatch.Elapsed.TotalSeconds;
            float deltaTime = Math.Min(currentTime - lastTime, 0.1f);
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
    }
}

