namespace ReiStar.VisualEditor.Mounting;

using System;
using SDL;
using reistar.Core;
using reistar.Maths;

/// <summary>
/// Virtual IWindow implementation attached to the ImGui Scene Viewport.
/// Routes viewport panel dimensions and viewport-local input into the game without window coupling.
/// </summary>
public class EditorViewportWindow : IWindow
{
    private string _title = "Editor Scene Viewport";
    private Vect2D _size = new(800, 600);
    private bool _isRunning = true;

    public string Title
    {
        get => _title;
        set => _title = value;
    }

    public Vect2D Size => _size;
    public bool IsRunning => _isRunning;

    public void SetViewportSize(int width, int height)
    {
        int clampedW = Math.Max(32, width);
        int clampedH = Math.Max(32, height);

        if ((int)_size.X != clampedW || (int)_size.Y != clampedH)
        {
            _size = new Vect2D(clampedW, clampedH);
        }
    }

    public void PollEvents()
    {
        // Viewport events are routed via ImGui panel focus & hover state
    }

    public ulong GetPerformanceCounter() => SDL3.SDL_GetPerformanceCounter();
    public ulong GetPerformanceFrequency() => SDL3.SDL_GetPerformanceFrequency();

    public void Stop()
    {
        _isRunning = false;
    }

    public void Dispose()
    {
        _isRunning = false;
    }
}
