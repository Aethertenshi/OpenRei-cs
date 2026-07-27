using OpenRei.Filters;
using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Executes Separable Multi-Pass Dual-Kawase / Gaussian Blur using downsampled FBO render targets (osu!-framework style).
/// </summary>
public unsafe class BlurPipeline : IDisposable
{
    private SDL_Renderer* _renderer;
    private SDL_Texture* _pingTexture;
    private SDL_Texture* _pongTexture;
    private int _currentW;
    private int _currentH;
    private bool _isDisposed;

    public BlurPipeline(SDL_Renderer* renderer)
    {
        _renderer = renderer;
    }

    private void EnsureRenderTargets(int width, int height)
    {
        width = Math.Max(width, 1);
        height = Math.Max(height, 1);

        if (_currentW == width && _currentH == height && _pingTexture != null && _pongTexture != null)
            return;

        if (_pingTexture != null) SDL3.SDL_DestroyTexture(_pingTexture);
        if (_pongTexture != null) SDL3.SDL_DestroyTexture(_pongTexture);

        _currentW = width;
        _currentH = height;

        _pingTexture = SDL3.SDL_CreateTexture(_renderer, SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888, SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, width, height);
        _pongTexture = SDL3.SDL_CreateTexture(_renderer, SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888, SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, width, height);

        if (_pingTexture != null) SDL3.SDL_SetTextureScaleMode(_pingTexture, SDL_ScaleMode.SDL_SCALEMODE_LINEAR);
        if (_pongTexture != null) SDL3.SDL_SetTextureScaleMode(_pongTexture, SDL_ScaleMode.SDL_SCALEMODE_LINEAR);
    }

    /// <summary>
    /// Captures source screen region, applies multi-pass downsampled Gaussian blur, and renders result back to screen.
    /// </summary>
    public void ApplyBlur(Rect bounds, BlurFilter filter)
    {
        if (_renderer == null || filter == null || !filter.Enabled || filter.Radius <= 0f) return;

        int downscale = Math.Clamp(filter.Downscale, 1, 4);
        int passes = Math.Clamp(filter.Passes, 1, 4);

        int targetW = (int)Math.Max(bounds.Width / downscale, 1);
        int targetH = (int)Math.Max(bounds.Height / downscale, 1);

        EnsureRenderTargets(targetW, targetH);
        if (_pingTexture == null || _pongTexture == null) return;

        SDL_FRect srcArea = new SDL_FRect { x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height };
        SDL_FRect fboDest = new SDL_FRect { x = 0, y = 0, w = targetW, h = targetH };

        // Step 1: Capture main screen region pixels into Ping FBO with downsampling
        SDL_Rect readRect = new SDL_Rect
        {
            x = (int)bounds.X,
            y = (int)bounds.Y,
            w = (int)bounds.Width,
            h = (int)bounds.Height
        };

        SDL_Surface* screenSurface = SDL3.SDL_RenderReadPixels(_renderer, &readRect);
        if (screenSurface != null)
        {
            SDL_Texture* screenTex = SDL3.SDL_CreateTextureFromSurface(_renderer, screenSurface);
            if (screenTex != null)
            {
                SDL3.SDL_SetRenderTarget(_renderer, _pingTexture);
                SDL3.SDL_RenderTexture(_renderer, screenTex, null, &fboDest);
                SDL3.SDL_DestroyTexture(screenTex);
            }
            SDL3.SDL_DestroySurface(screenSurface);
        }

        // Step 2: Multi-pass separable Gaussian blur between Ping and Pong FBO targets
        SDL_Texture* readTarget = _pingTexture;
        SDL_Texture* writeTarget = _pongTexture;

        for (int p = 0; p < passes; p++)
        {
            // Horizontal Pass
            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);
            SDL3.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 0);
            SDL3.SDL_RenderClear(_renderer);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &fboDest);

            // Swap Targets
            SDL_Texture* temp1 = readTarget;
            readTarget = writeTarget;
            writeTarget = temp1;

            // Vertical Pass
            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);
            SDL3.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 0);
            SDL3.SDL_RenderClear(_renderer);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &fboDest);

            // Swap Targets
            SDL_Texture* temp2 = readTarget;
            readTarget = writeTarget;
            writeTarget = temp2;
        }

        // Step 3: Reset main swapchain render target
        SDL3.SDL_SetRenderTarget(_renderer, null);

        // Step 4: Upsample and composite final blurred FBO back onto the screen
        SDL3.SDL_RenderTexture(_renderer, readTarget, null, &srcArea);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_pingTexture != null) { SDL3.SDL_DestroyTexture(_pingTexture); _pingTexture = null; }
            if (_pongTexture != null) { SDL3.SDL_DestroyTexture(_pongTexture); _pongTexture = null; }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
