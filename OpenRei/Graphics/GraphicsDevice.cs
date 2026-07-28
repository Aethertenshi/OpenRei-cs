using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Controls hardware-accelerated 2D GPU rendering.
/// Supports both SDL_Renderer (legacy) and SDL_GPU (new) backends side by side.
/// </summary>
public unsafe class GraphicsDevice : IDisposable
{
    // ── SDL_Renderer backend (legacy, kept for coexistence) ─────────────────────
    private SDL_Renderer* _renderer;
    private SDL_Window* _window;
    private bool _isDisposed;

    private BlurPipeline? _blurPipeline;
    private GpuRenderer? _gpuRenderer;

    public SDL_Renderer* RendererHandle => _renderer;
    public bool IsRendererReady => _renderer != null;
    public bool IsInitialized => IsRendererReady; // compat alias for TextureEngine

    public GpuRenderer? GpuRendererInstance => _gpuRenderer;

    // ── SDL_GPU backend ────────────────────────────────────────────────────────
    private SDL_GPUDevice* _gpuDevice;
    private ShaderPipeline? _shaderPipeline;
    private SDL_GPUTextureFormat _swapchainFormat;

    public SDL_GPUDevice* GpuDevice => _gpuDevice;
    public ShaderPipeline? Pipelines => _shaderPipeline;
    public SDL_GPUTextureFormat SwapchainFormat => _swapchainFormat;
    public bool IsGpuReady => _gpuDevice != null;

    public GraphicsDevice(SDL_Window* window)
    {
        _window = window;

        // ── Initialize SDL_GPU backend (primary) ───────────────────────────────
        InitGpu();

        // ── Initialize SDL_Renderer backend (fallback if GPU unavailable) ──────
        if (!IsGpuReady)
        {
            _renderer = SDL3.SDL_CreateRenderer(_window, (byte*)null);
            if (_renderer != null)
            {
                SDL3.SDL_SetRenderVSync(_renderer, 1);
                SDL3.SDL_SetRenderDrawBlendMode(_renderer, SDL_BlendMode.SDL_BLENDMODE_BLEND);
                Console.WriteLine("[GraphicsDevice] SDL_Renderer initialized (GPU unavailable).");
            }
            else
            {
                Console.WriteLine($"[GraphicsDevice] SDL_Renderer not available: {SDL3.SDL_GetError()}");
            }
        }

        // ── Shared subsystems ──────────────────────────────────────────────────
        FontEngine.Initialize();

        if (_renderer != null)
        {
            _blurPipeline = new BlurPipeline(_renderer);
            Console.WriteLine("[GraphicsDevice] BlurPipeline initialized (SDL_Renderer).");
        }
    }

    private void InitGpu()
    {
        // Try each shader format
        var formats = new[] {
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL,
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL
        };
        foreach (var fmt in formats)
        {
            _gpuDevice = SDL3.SDL_CreateGPUDevice(fmt, false, (byte*)null);
            if (_gpuDevice != null) break;
        }
        if (_gpuDevice == null)
        {
            _gpuDevice = SDL3.SDL_CreateGPUDevice(
                SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV |
                SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL |
                SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL,
                false, (byte*)null);
        }
        if (_gpuDevice == null)
        {
            Console.WriteLine("[GraphicsDevice] SDL_GPU not available — falling back to SDL_Renderer only.");
            return;
        }

        if (!SDL3.SDL_ClaimWindowForGPUDevice(_gpuDevice, _window))
        {
            Console.WriteLine($"[GraphicsDevice] SDL_GPU claim window failed: {SDL3.SDL_GetError()}");
            SDL3.SDL_DestroyGPUDevice(_gpuDevice);
            _gpuDevice = null;
            return;
        }

        SDL3.SDL_SetGPUSwapchainParameters(_gpuDevice, _window,
            SDL_GPUSwapchainComposition.SDL_GPU_SWAPCHAINCOMPOSITION_SDR,
            SDL_GPUPresentMode.SDL_GPU_PRESENTMODE_VSYNC);

        _swapchainFormat = SDL3.SDL_GetGPUSwapchainTextureFormat(_gpuDevice, _window);
        Console.WriteLine($"[GraphicsDevice] SDL_GPU ready. Swapchain format: {_swapchainFormat}");

        _shaderPipeline = new ShaderPipeline(_gpuDevice);
        if (!_shaderPipeline.CreatePipelines(_swapchainFormat))
        {
            Console.WriteLine("[GraphicsDevice] ShaderPipeline creation failed — GPU rendering disabled.");
            SDL3.SDL_ReleaseWindowFromGPUDevice(_gpuDevice, _window);
            SDL3.SDL_DestroyGPUDevice(_gpuDevice);
            _gpuDevice = null;
            _shaderPipeline = null;
        }
        else
        {
            _gpuRenderer = new GpuRenderer(_gpuDevice, _window, _shaderPipeline, _swapchainFormat);
            Console.WriteLine("[GraphicsDevice] GpuRenderer ready.");
        }
    }

    // ── Legacy SDL_Renderer text/image rendering (kept for BlurPipeline compat) ─

    public void RenderText(Font? font, string text, Rect bounds, Color color)
    {
        font ??= FontEngine.DefaultFont;
        if (!IsRendererReady || _renderer == null || font == null || font.Handle == null || string.IsNullOrEmpty(text)) return;

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
        if (!IsRendererReady || _renderer == null || texture == null || !texture.IsValid) return;
        SDL_FRect dest = new SDL_FRect { x = destBounds.X, y = destBounds.Y, w = destBounds.Width, h = destBounds.Height };
        SDL3.SDL_SetTextureBlendMode(texture.Handle, SDL_BlendMode.SDL_BLENDMODE_BLEND);
        SDL3.SDL_SetTextureColorModFloat(texture.Handle, Math.Clamp(color.R, 0f, 1f), Math.Clamp(color.G, 0f, 1f), Math.Clamp(color.B, 0f, 1f));
        SDL3.SDL_SetTextureAlphaModFloat(texture.Handle, Math.Clamp(color.A, 0f, 1f));
        if (sourceRect.HasValue)
        {
            SDL_FRect src = new SDL_FRect { x = sourceRect.Value.X, y = sourceRect.Value.Y, w = sourceRect.Value.Width, h = sourceRect.Value.Height };
            SDL3.SDL_RenderTexture(_renderer, texture.Handle, &src, &dest);
        }
        else
        {
            SDL3.SDL_RenderTexture(_renderer, texture.Handle, null, &dest);
        }
    }

    // ── Render pass (SDL_Renderer path, kept for backward compat) ──────────────

    public void RenderPass(RenderQueue queue, RenderContext context)
    {
        if (!IsRendererReady || _renderer == null) return; // GPU-only mode, skip SDL_Renderer path

        TextureEngine.ProcessPendingUploads(this, 2.0f);
        context.RendererHandle = _renderer;

        SDL3.SDL_SetRenderDrawColor(_renderer, 18, 18, 24, 255);
        SDL3.SDL_RenderClear(_renderer);

        int quadCount = queue.ActiveReadCount;
        QuadInstance* instances = queue.ActiveReadBuffer;
        int totalCount = quadCount + context.ImageCommands.Count + context.TextCommands.Count;

        if (totalCount > 0)
        {
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

            foreach (var key in sortKeys)
            {
                switch (key.Type)
                {
                    case 0: // Quad
                        var quad = instances[key.Index];
                        SDL_FRect rect = new SDL_FRect { x = quad.Bounds.X, y = quad.Bounds.Y, w = quad.Bounds.Width, h = quad.Bounds.Height };
                        SDL3.SDL_SetRenderDrawColorFloat(_renderer, quad.Color.R, quad.Color.G, quad.Color.B, quad.Color.A);
                        SDL3.SDL_RenderFillRect(_renderer, &rect);
                        break;
                    case 1: // Image
                        var imgCmd = context.ImageCommands[key.Index];
                        if (imgCmd.BlurFilter != null && imgCmd.BlurFilter.Enabled && imgCmd.BlurFilter.Radius > 0.05f)
                            _blurPipeline?.RenderBlurredTexture(imgCmd.Texture, imgCmd.DestBounds, imgCmd.SourceRect, imgCmd.Color, imgCmd.BlurFilter);
                        else
                            RenderImage(imgCmd.Texture, imgCmd.DestBounds, imgCmd.SourceRect, imgCmd.Color);
                        break;
                    case 2: // Text
                        var textCmd = context.TextCommands[key.Index];
                        RenderText(textCmd.Font, textCmd.Text, textCmd.Bounds, textCmd.Color);
                        break;
                }
            }
        }

        SDL3.SDL_RenderPresent(_renderer);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _gpuRenderer?.Dispose();
            _shaderPipeline?.Dispose();
            _blurPipeline?.Dispose();
            if (_gpuDevice != null)
            {
                SDL3.SDL_WaitForGPUIdle(_gpuDevice);
                SDL3.SDL_ReleaseWindowFromGPUDevice(_gpuDevice, _window);
                SDL3.SDL_DestroyGPUDevice(_gpuDevice);
                _gpuDevice = null;
            }
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
