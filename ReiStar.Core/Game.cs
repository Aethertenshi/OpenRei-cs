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

    private bool _isInitialized;

    public void Initialize()
    {
        if (!_isInitialized)
        {
            OnInitialize();
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Executes a single frame tick (Update + Points Update + Render + Points Render).
    /// Used by Visual Editors and external host runners.
    /// </summary>
    public void Step(float deltaTime)
    {
        if (!_isInitialized)
        {
            Initialize();
        }

        OnUpdate(deltaTime);
        Points.UpdatePoints(deltaTime);

        Renderer.BeginFrame();
        OnRender();
        Points.RenderPoints();
        Renderer.EndFrame();
    }

    public void Run()
    {
        Initialize();

        ulong lastCounter = Window.GetPerformanceCounter();

        while (Window.IsRunning)
        {
            ulong frameStartCounter = Window.GetPerformanceCounter();
            ulong frequency = Window.GetPerformanceFrequency();
            ulong deltaTicks = frameStartCounter - lastCounter;
            lastCounter = frameStartCounter;

            Time.Update(deltaTicks, frequency > 0 ? frequency : 10000000);

            Window.PollEvents();

            Step(Time.DeltaTime);

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

