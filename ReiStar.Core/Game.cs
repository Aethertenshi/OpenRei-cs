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


    public int TargetFPS
    {
        get => Time.TargetFPS;
        set => Time.TargetFPS = value;
    }

    public bool VSync
    {
        get => Renderer.VSync;
        set => Renderer.VSync = value;
    }

    public void Run()
    {
        OnInitialize();

        ulong lastCounter = Window.GetPerformanceCounter();

        while (Window.IsRunning)
        {
            ulong frameStartCounter = Window.GetPerformanceCounter();
            ulong frequency = Window.GetPerformanceFrequency();
            ulong deltaTicks = frameStartCounter - lastCounter;
            lastCounter = frameStartCounter;

            Time.Update(deltaTicks, frequency > 0 ? frequency : 10000000);

            Window.PollEvents();

            OnUpdate(Time.DeltaTime);
            Points.UpdatePoints(Time.DeltaTime);

            Renderer.BeginFrame();
            OnRender();
            Points.RenderPoints();
            Renderer.EndFrame();

            ulong frameEndCounter = Window.GetPerformanceCounter();
            Time.ThrottleFrame(frameStartCounter, frameEndCounter, frequency > 0 ? frequency : 10000000);
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

