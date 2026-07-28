using OpenRei.Core;
using OpenRei.Filters;
using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Executes Hardware-Accelerated Dual-Kawase / Gaussian Blur (osu!-framework style)
/// directly on element textures using normalized 4-tap sub-pixel kernel sampling over high-precision FBO render targets.
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

        if (_pingTexture != null)
        {
            SDL3.SDL_SetTextureScaleMode(_pingTexture, SDL_ScaleMode.SDL_SCALEMODE_LINEAR);
            SDL3.SDL_SetTextureBlendMode(_pingTexture, SDL_BlendMode.SDL_BLENDMODE_NONE);
        }
        if (_pongTexture != null)
        {
            SDL3.SDL_SetTextureScaleMode(_pongTexture, SDL_ScaleMode.SDL_SCALEMODE_LINEAR);
            SDL3.SDL_SetTextureBlendMode(_pongTexture, SDL_BlendMode.SDL_BLENDMODE_NONE);
        }
    }

    /// <summary>
    /// Renders a texture directly onto the screen with Multi-Pass Gaussian Blur applied ONLY to the texture itself.
    /// </summary>
    public void RenderBlurredTexture(Texture texture, Rect destBounds, Rect? sourceRect, Color color, BlurFilter filter)
    {
        if (_renderer == null || texture == null || !texture.IsValid || filter == null || !filter.Enabled || filter.Radius <= 0.05f) return;

        int downscale = (filter.Radius < 6.0f) ? 1 : 2;
        int passes = Math.Clamp(filter.Passes, 1, 4);

        int targetW = (int)Math.Max(destBounds.Width / downscale, 1);
        int targetH = (int)Math.Max(destBounds.Height / downscale, 1);

        EnsureRenderTargets(targetW, targetH);
        if (_pingTexture == null || _pongTexture == null) return;

        SDL_FRect destArea = new SDL_FRect
        {
            x = destBounds.X, y = destBounds.Y,
            w = destBounds.Width, h = destBounds.Height
        };
        SDL_FRect fboDest = new SDL_FRect { x = 0, y = 0, w = targetW, h = targetH };

        // Step 1: Render source texture directly into Ping FBO (NOT screen capture!)
        SDL3.SDL_SetTextureBlendMode(texture.Handle, SDL_BlendMode.SDL_BLENDMODE_NONE);
        SDL3.SDL_SetRenderTarget(_renderer, _pingTexture);
        if (sourceRect.HasValue)
        {
            SDL_FRect src = new SDL_FRect
            {
                x = sourceRect.Value.X, y = sourceRect.Value.Y,
                w = sourceRect.Value.Width, h = sourceRect.Value.Height
            };
            SDL3.SDL_RenderTexture(_renderer, texture.Handle, &src, &fboDest);
        }
        else
        {
            SDL3.SDL_RenderTexture(_renderer, texture.Handle, null, &fboDest);
        }

        // Step 2: Multi-Pass Normalized 4-Tap Gaussian Kernel Sampling on Texture FBO
        SDL_Texture* readTarget = _pingTexture;
        SDL_Texture* writeTarget = _pongTexture;

        float kernelSpread = (filter.Radius / (float)downscale) / (float)passes * 0.4f;

        for (int p = 0; p < passes; p++)
        {
            float offset = (p + 1.0f) * kernelSpread;

            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);

            // Tap 1: Top-Left (-offset, -offset) - Base Overwrite (1.0 weight)
            SDL_FRect t1 = new SDL_FRect { x = -offset, y = -offset, w = targetW, h = targetH };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_NONE);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 1.0f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &t1);

            // Tap 2: Top-Right (+offset, -offset) - 50% blend
            SDL_FRect t2 = new SDL_FRect { x = offset, y = -offset, w = targetW, h = targetH };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.50f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &t2);

            // Tap 3: Bottom-Left (-offset, +offset) - 33.3% blend
            SDL_FRect t3 = new SDL_FRect { x = -offset, y = offset, w = targetW, h = targetH };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.333333f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &t3);

            // Tap 4: Bottom-Right (+offset, +offset) - 25% blend
            SDL_FRect t4 = new SDL_FRect { x = offset, y = offset, w = targetW, h = targetH };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.25f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &t4);

            // Swap Ping-Pong targets
            SDL_Texture* temp = readTarget;
            readTarget = writeTarget;
            writeTarget = temp;
        }

        // Step 3: Reset main swapchain render target back to main screen
        SDL3.SDL_SetRenderTarget(_renderer, null);

        // Step 4: Configure final blend mode & color modulation for dynamic radius tweening
        if (filter.Radius < 3.0f)
        {
            float blurOpacity = Math.Clamp(filter.Radius / 3.0f, 0.0f, 1.0f);
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureColorModFloat(readTarget, Math.Clamp(color.R, 0f, 1f), Math.Clamp(color.G, 0f, 1f), Math.Clamp(color.B, 0f, 1f));
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, Math.Clamp(color.A, 0f, 1f) * blurOpacity);
        }
        else
        {
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureColorModFloat(readTarget, Math.Clamp(color.R, 0f, 1f), Math.Clamp(color.G, 0f, 1f), Math.Clamp(color.B, 0f, 1f));
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, Math.Clamp(color.A, 0f, 1f));
        }

        // Step 5: Render final blurred texture onto screen
        SDL3.SDL_RenderTexture(_renderer, readTarget, null, &destArea);
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
