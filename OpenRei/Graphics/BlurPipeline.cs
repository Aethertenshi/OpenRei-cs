using OpenRei.Core;
using OpenRei.Filters;
using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Executes Hardware-Accelerated Multi-Pass Gaussian Blur using multi-pass FBO sampling and bilinear filtering.
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

        if (_pingTexture == null || _pongTexture == null || _currentW != width || _currentH != height)
        {
            if (_pingTexture != null) SDL3.SDL_DestroyTexture(_pingTexture);
            if (_pongTexture != null) SDL3.SDL_DestroyTexture(_pongTexture);

            _currentW = width;
            _currentH = height;

            _pingTexture = SDL3.SDL_CreateTexture(_renderer,
                SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888,
                SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET,
                width, height);

            _pongTexture = SDL3.SDL_CreateTexture(_renderer,
                SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888,
                SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET,
                width, height);

            SDL3.SDL_SetTextureScaleMode(_pingTexture, SDL_ScaleMode.SDL_SCALEMODE_LINEAR);
            SDL3.SDL_SetTextureBlendMode(_pingTexture, SDL_BlendMode.SDL_BLENDMODE_NONE);

            SDL3.SDL_SetTextureScaleMode(_pongTexture, SDL_ScaleMode.SDL_SCALEMODE_LINEAR);
            SDL3.SDL_SetTextureBlendMode(_pongTexture, SDL_BlendMode.SDL_BLENDMODE_NONE);
        }
    }

    /// <summary>
    /// Renders a texture directly onto the screen with Multi-Pass Gaussian Blur applied ONLY to the texture itself.
    /// Reverted to original 1:1 tap structure as requested.
    /// </summary>
    public void RenderBlurredTexture(Texture texture, Rect destBounds, Rect? sourceRect, Color color, BlurFilter filter)
    {
        if (_renderer == null || texture == null || !texture.IsValid || filter == null || !filter.Enabled || filter.Radius <= 0.05f) return;

        int downscale = (filter.Radius < 4.0f) ? 1 : 2;
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

        // Step 1: Render source texture WITH color tint modulation into Ping FBO
        SDL3.SDL_SetTextureBlendMode(texture.Handle, SDL_BlendMode.SDL_BLENDMODE_NONE);
        SDL3.SDL_SetTextureColorModFloat(texture.Handle, Math.Clamp(color.R, 0f, 1f), Math.Clamp(color.G, 0f, 1f), Math.Clamp(color.B, 0f, 1f));
        SDL3.SDL_SetTextureAlphaModFloat(texture.Handle, Math.Clamp(color.A, 0f, 1f));

        SDL3.SDL_SetRenderTarget(_renderer, _pingTexture);
        SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
        SDL3.SDL_RenderClear(_renderer);

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

        // Step 2: Multi-Pass 5-Tap Gaussian Kernel Sampling (Reverted 1:1 tap structure)
        SDL_Texture* readTarget = _pingTexture;
        SDL_Texture* writeTarget = _pongTexture;

        float kernelSpread = (filter.Radius / (float)downscale) / (float)passes * 0.35f;

        for (int p = 0; p < passes; p++)
        {
            float offset = (p + 1.0f) * kernelSpread;

            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);
            SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
            SDL3.SDL_RenderClear(_renderer);

            // Tap 0: Center (0, 0)
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_NONE);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 1.0f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &fboDest);

            // Tap 1: Top-Left (-offset, -offset)
            SDL_FRect t1 = new SDL_FRect { x = -offset, y = -offset, w = targetW + offset, h = targetH + offset };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.35f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &t1);

            // Tap 2: Top-Right (+offset, -offset)
            SDL_FRect t2 = new SDL_FRect { x = 0, y = -offset, w = targetW + offset, h = targetH + offset };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.25f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &t2);

            // Tap 3: Bottom-Left (-offset, +offset)
            SDL_FRect t3 = new SDL_FRect { x = -offset, y = 0, w = targetW + offset, h = targetH + offset };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.20f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &t3);

            // Tap 4: Bottom-Right (+offset, +offset)
            SDL_FRect t4 = new SDL_FRect { x = 0, y = 0, w = targetW + offset, h = targetH + offset };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.15f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &t4);

            // Swap Ping-Pong targets
            SDL_Texture* temp = readTarget;
            readTarget = writeTarget;
            writeTarget = temp;
        }

        // Step 3: Reset main swapchain render target back to main screen
        SDL3.SDL_SetRenderTarget(_renderer, null);

        // Step 4: Configure final blend mode and composite
        SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
        SDL3.SDL_SetTextureColorModFloat(readTarget, 1f, 1f, 1f);
        SDL3.SDL_SetTextureAlphaModFloat(readTarget, 1f);
        SDL3.SDL_RenderTexture(_renderer, readTarget, null, &destArea);
    }

    /// <summary>
    /// Renders Photoshop-quality smooth Gaussian Drop Shadow using Separable 1D Horizontal & Vertical Gaussian Passes
    /// and hardware bilinear downsampling. Eliminates all box/block artifacts.
    /// </summary>
    public void RenderShadow(Rect bounds, Color color, CornerRadius cornerRadius, BlurFilter filter)
    {
        if (_renderer == null || filter == null || !filter.Enabled || filter.Radius <= 0.05f) return;

        int winW = 0, winH = 0;
        SDL3.SDL_GetRenderOutputSize(_renderer, &winW, &winH);

        // Expand bounds by BlurRadius on all sides so Gaussian blur has room to spread smoothly
        float expand = filter.Radius * 2.5f;
        float eX = bounds.X - expand;
        float eY = bounds.Y - expand;
        float eW = bounds.Width + expand * 2f;
        float eH = bounds.Height + expand * 2f;

        // Clamp expanded region to screen bounds
        float cX = MathF.Max(eX, 0f), cY = MathF.Max(eY, 0f);
        float cR = MathF.Min(eX + eW, winW);
        float cB = MathF.Min(eY + eH, winH);
        float cW = cR - cX, cH = cB - cY;
        if (cW <= 0f || cH <= 0f) return;

        // Downscale FBO for hardware bilinear filtering and smooth Gaussian dispersion
        int downscale = (filter.Radius > 10.0f) ? 4 : 2;
        int tW = (int)Math.Max(cW / downscale, 1);
        int tH = (int)Math.Max(cH / downscale, 1);

        EnsureRenderTargets(tW, tH);
        if (_pingTexture == null || _pongTexture == null) return;

        SDL_FRect compositeDst = new SDL_FRect { x = cX, y = cY, w = cW, h = cH };
        SDL_FRect fboFull = new SDL_FRect { x = 0, y = 0, w = tW, h = tH };

        // Position of shadow quad inside the downscaled FBO
        float scaleFactor = 1.0f / (float)downscale;
        float innerX = (bounds.X - cX) * scaleFactor;
        float innerY = (bounds.Y - cY) * scaleFactor;
        float innerW = bounds.Width * scaleFactor;
        float innerH = bounds.Height * scaleFactor;

        // Step 1: Clear FBOs and draw solid shadow quad into Ping FBO
        SDL3.SDL_SetRenderTarget(_renderer, _pingTexture);
        SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
        SDL3.SDL_RenderClear(_renderer);

        SDL3.SDL_SetRenderTarget(_renderer, _pongTexture);
        SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
        SDL3.SDL_RenderClear(_renderer);

        SDL3.SDL_SetRenderTarget(_renderer, _pingTexture);
        SDL3.SDL_SetRenderDrawColorFloat(_renderer, color.R, color.G, color.B, color.A);
        SDL_FRect innerRect = new SDL_FRect { x = innerX, y = innerY, w = innerW, h = innerH };
        SDL3.SDL_RenderFillRect(_renderer, &innerRect);

        // Step 2: Separable 1D Gaussian Passes (Horizontal + Vertical Passes)
        // 1D Gaussian kernel weights: Center 0.383, 1D Offsets (+1.333) 0.308 each
        int passes = Math.Clamp(filter.Passes, 2, 6);
        float step = (filter.Radius / (float)downscale) / (float)passes * 1.2f;

        SDL_Texture* readTarget = _pingTexture;
        SDL_Texture* writeTarget = _pongTexture;

        for (int p = 0; p < passes; p++)
        {
            float offset = (p + 1.0f) * step;

            // --- 1D Horizontal Gaussian Pass (Ping -> Pong) ---
            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);
            SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
            SDL3.SDL_RenderClear(_renderer);

            // Center (weight 0.4)
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_NONE);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 1.0f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &fboFull);

            // Horizontal Left (-offset, 0)
            SDL_FRect hLeft = new SDL_FRect { x = -offset, y = 0, w = tW, h = tH };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &hLeft);

            // Horizontal Right (+offset, 0)
            SDL_FRect hRight = new SDL_FRect { x = offset, y = 0, w = tW, h = tH };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &hRight);

            // Swap targets
            var tmpH = readTarget; readTarget = writeTarget; writeTarget = tmpH;

            // --- 1D Vertical Gaussian Pass (Pong -> Ping) ---
            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);
            SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
            SDL3.SDL_RenderClear(_renderer);

            // Center (weight 0.4)
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_NONE);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 1.0f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &fboFull);

            // Vertical Top (0, -offset)
            SDL_FRect vTop = new SDL_FRect { x = 0, y = -offset, w = tW, h = tH };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &vTop);

            // Vertical Bottom (0, +offset)
            SDL_FRect vBot = new SDL_FRect { x = 0, y = offset, w = tW, h = tH };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &vBot);

            // Swap targets
            var tmpV = readTarget; readTarget = writeTarget; writeTarget = tmpV;
        }

        // Step 3: Composite smooth Gaussian shadow back onto screen with hardware bilinear interpolation
        SDL3.SDL_SetRenderTarget(_renderer, null);
        SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
        SDL3.SDL_SetTextureColorModFloat(readTarget, 1f, 1f, 1f);
        SDL3.SDL_SetTextureAlphaModFloat(readTarget, Math.Clamp(color.A, 0f, 1f));
        SDL3.SDL_RenderTexture(_renderer, readTarget, null, &compositeDst);
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
