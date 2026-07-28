using OpenRei.Types;
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

    private BlurPipeline? _blurPipeline;

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

        // Ensure alpha blending is enabled for RenderFillRect (Quad alpha, splash fades)
        SDL3.SDL_SetRenderDrawBlendMode(_renderer, SDL_BlendMode.SDL_BLENDMODE_BLEND);

        // Initialize FontEngine and default font
        FontEngine.Initialize();

        // Initialize Multi-Pass Gaussian Blur FBO Pipeline
        _blurPipeline = new BlurPipeline(_renderer);

        Console.WriteLine("[GraphicsDevice] Hardware-Accelerated 2D GPU Renderer & BlurPipeline initialized successfully.");
    }

    /// <summary>
    /// Renders text using SDL3_ttf blended font surface.
    /// </summary>
    public void RenderText(Font? font, string text, Rect bounds, Color color)
    {
        font ??= FontEngine.DefaultFont;
        if (!IsInitialized || _renderer == null || font == null || font.Handle == null || string.IsNullOrEmpty(text)) return;

        byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(text + "\0");
        SDL_Color fgColor = new SDL_Color
        {
            r = (byte)(color.R * 255),
            g = (byte)(color.G * 255),
            b = (byte)(color.B * 255),
            a = (byte)(color.A * 255)
        };

        fixed (byte* tPtr = textBytes)
        {
            SDL_Surface* surface = SDL3_ttf.TTF_RenderText_Blended(font.Handle, tPtr, (nuint)text.Length, fgColor);
            if (surface == null) return;

            SDL_Texture* texture = SDL3.SDL_CreateTextureFromSurface(_renderer, surface);
            float textW = surface->w;
            float textH = surface->h;
            SDL3.SDL_DestroySurface(surface);

            if (texture != null)
            {
                // Center text inside bounds
                float posX = bounds.X + (bounds.Width - textW) * 0.5f;
                float posY = bounds.Y + (bounds.Height - textH) * 0.5f;

                SDL_FRect destRect = new SDL_FRect { x = posX, y = posY, w = textW, h = textH };
                SDL3.SDL_RenderTexture(_renderer, texture, null, &destRect);
                SDL3.SDL_DestroyTexture(texture);
            }
        }
    }
    public void RenderImage(Texture texture, Rect destBounds, Rect? sourceRect, Color color)
    {
        if (!IsInitialized || _renderer == null || texture == null || !texture.IsValid) return;

        SDL_FRect dest = new SDL_FRect
        {
            x = destBounds.X,
            y = destBounds.Y,
            w = destBounds.Width,
            h = destBounds.Height
        };

        SDL3.SDL_SetTextureBlendMode(texture.Handle, SDL_BlendMode.SDL_BLENDMODE_BLEND);
        SDL3.SDL_SetTextureColorModFloat(texture.Handle, color.R, color.G, color.B);
        SDL3.SDL_SetTextureAlphaModFloat(texture.Handle, color.A);

        if (sourceRect.HasValue)
        {
            SDL_FRect src = new SDL_FRect
            {
                x = sourceRect.Value.X,
                y = sourceRect.Value.Y,
                w = sourceRect.Value.Width,
                h = sourceRect.Value.Height
            };
            SDL3.SDL_RenderTexture(_renderer, texture.Handle, &src, &dest);
        }
        else
        {
            SDL3.SDL_RenderTexture(_renderer, texture.Handle, null, &dest);
        }
    }

    public void RenderPass(RenderQueue queue, RenderContext context)
    {
        if (!IsInitialized || _renderer == null) return;

        // Process background texture uploads within 2.0ms main-thread frame budget
        TextureEngine.ProcessPendingUploads(this, 2.0f);

        // Attach renderer handle to context for inline clip rect operations
        context.RendererHandle = _renderer;

        // 1. Clear background to dark theme color
        SDL3.SDL_SetRenderDrawColor(_renderer, 18, 18, 24, 255);
        SDL3.SDL_RenderClear(_renderer);

        // 2. Collect all render commands with ZIndex into a single sorted list
        int quadCount = queue.ActiveReadCount;
        QuadInstance* instances = queue.ActiveReadBuffer;

        int totalCount = quadCount + context.ImageCommands.Count + context.TextCommands.Count;
        if (totalCount == 0)
        {
            SDL3.SDL_RenderPresent(_renderer);
            return;
        }

        var sortKeys = new (float ZIndex, int BatchOrder, int Type, int Index)[totalCount];
        int sortIdx = 0;

        for (int i = 0; i < quadCount; i++)
            sortKeys[sortIdx++] = (instances[i].ZIndex, sortIdx, 0, i);

        for (int i = 0; i < context.ImageCommands.Count; i++)
            sortKeys[sortIdx++] = (context.ImageCommands[i].ZIndex, sortIdx, 1, i);

        for (int i = 0; i < context.TextCommands.Count; i++)
            sortKeys[sortIdx++] = (context.TextCommands[i].ZIndex, sortIdx, 2, i);

        Array.Sort(sortKeys, (a, b) =>
        {
            int cmp = a.ZIndex.CompareTo(b.ZIndex);
            return cmp != 0 ? cmp : a.BatchOrder.CompareTo(b.BatchOrder);
        });

        // 3. Render all commands in ZIndex order
        foreach (var key in sortKeys)
        {
            switch (key.Type)
            {
                case 0: // Quad
                    {
                        var quad = instances[key.Index];
                        SDL_FRect rect = new SDL_FRect
                        {
                            x = quad.Bounds.X, y = quad.Bounds.Y,
                            w = quad.Bounds.Width, h = quad.Bounds.Height
                        };
                        SDL3.SDL_SetRenderDrawColorFloat(_renderer, quad.Color.R, quad.Color.G, quad.Color.B, quad.Color.A);
                        SDL3.SDL_RenderFillRect(_renderer, &rect);
                        break;
                    }
                case 1: // Image
                    {
                        var imgCmd = context.ImageCommands[key.Index];
                        RenderImage(imgCmd.Texture, imgCmd.DestBounds, imgCmd.SourceRect, imgCmd.Color);
                        break;
                    }
                case 2: // Text
                    {
                        var textCmd = context.TextCommands[key.Index];
                        RenderText(textCmd.Font, textCmd.Text, textCmd.Bounds, textCmd.Color);
                        break;
                    }
            }
        }

        // 4. Execute Multi-pass Gaussian Blur Pipeline
        foreach (var blurCmd in context.BlurCommands)
            _blurPipeline?.ApplyBlur(blurCmd.Bounds, blurCmd.Filter);

        // 5. Swap buffers (Present frame to window display)
        SDL3.SDL_RenderPresent(_renderer);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _blurPipeline?.Dispose();
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
