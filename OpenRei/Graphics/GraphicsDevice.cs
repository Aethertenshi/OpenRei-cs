using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Controls hardware-accelerated 2D GPU rendering, swapchain presentation, and pipeline execution.
/// </summary>
public unsafe class GraphicsDevice : IDisposable
{
    private SDL_Renderer* _renderer;
    private SDL_Window* _window;
    private bool _isDisposed;

    public SDL_Renderer* RendererHandle => _renderer;
    public bool IsInitialized => _renderer != null;

    public GraphicsDevice(SDL_Window* window)
    {
        _window = window;

        // Create Hardware-Accelerated SDL3 Renderer (Vulkan / Direct3D 12 / Metal)
        _renderer = SDL3.SDL_CreateRenderer(_window, (byte*)null);

        if (_renderer == null)
        {
            Console.WriteLine($"[SDL3 Warning] Could not initialize native GPU renderer: {SDL3.SDL_GetError()}");
            return;
        }

        // Enable VSync / Mailbox presentation mode
        SDL3.SDL_SetRenderVSync(_renderer, 1);

        Console.WriteLine("[GraphicsDevice] Hardware-Accelerated 2D GPU Renderer initialized successfully.");
    }

    /// <summary>
    /// Executes a hardware GPU render pass consuming QuadInstances from the RenderQueue.
    /// </summary>
    public void RenderPass(RenderQueue queue)
    {
        if (!IsInitialized || _renderer == null) return;

        // 1. Clear background to dark theme color
        SDL3.SDL_SetRenderDrawColor(_renderer, 18, 18, 24, 255);
        SDL3.SDL_RenderClear(_renderer);

        // 2. Render all QuadInstances from persistent NativeMemory buffer
        int instanceCount = queue.ActiveReadCount;
        QuadInstance* instances = queue.ActiveReadBuffer;

        for (int i = 0; i < instanceCount; i++)
        {
            var quad = instances[i];

            SDL_FRect rect = new SDL_FRect
            {
                x = quad.Bounds.X,
                y = quad.Bounds.Y,
                w = quad.Bounds.Width,
                h = quad.Bounds.Height
            };

            SDL3.SDL_SetRenderDrawColorFloat(_renderer, quad.Color.R, quad.Color.G, quad.Color.B, quad.Color.A);
            SDL3.SDL_RenderFillRect(_renderer, &rect);
        }

        // 3. Swap buffers (Present frame to window display)
        SDL3.SDL_RenderPresent(_renderer);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_renderer != null)
            {
                SDL3.SDL_DestroyRenderer(_renderer);
                _renderer = null;
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
