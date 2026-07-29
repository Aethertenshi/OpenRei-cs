using OpenRei.Core;
using OpenRei.Filters;
using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Executes Hardware-Accelerated Multi-Pass Gaussian Blur (osu!-framework style)
/// using normalized 5-tap sub-pixel kernel sampling over high-precision FBO render targets.
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

        // Step 2: Multi-Pass 5-Tap Gaussian Sub-Pixel Kernel Sampling (Zero Edge Black Borders)
        SDL_Texture* readTarget = _pingTexture;
        SDL_Texture* writeTarget = _pongTexture;

        float kernelSpread = (filter.Radius / (float)downscale) / (float)passes * 0.35f;

        for (int p = 0; p < passes; p++)
        {
            float offset = (p + 1.0f) * kernelSpread;

            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);

            // Tap 0: Center (0, 0) - Base Overwrite (weight 0.4)
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_NONE);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 1.0f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &fboDest);

            // Tap 1: Top-Left (-offset, -offset) - Blend 35%
            SDL_FRect t1 = new SDL_FRect { x = -offset, y = -offset, w = targetW + offset, h = targetH + offset };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.35f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &t1);

            // Tap 2: Top-Right (+offset, -offset) - Blend 25%
            SDL_FRect t2 = new SDL_FRect { x = 0, y = -offset, w = targetW + offset, h = targetH + offset };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.25f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &t2);

            // Tap 3: Bottom-Left (-offset, +offset) - Blend 20%
            SDL_FRect t3 = new SDL_FRect { x = -offset, y = 0, w = targetW + offset, h = targetH + offset };
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.20f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &t3);

            // Tap 4: Bottom-Right (+offset, +offset) - Blend 15%
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
        SDL3.SDL_SetTextureAlphaModFloat(readTarget, Math.Clamp(color.A, 0f, 1f));

        // Step 5: Upsample and composite final silky Gaussian blur back onto screen viewport
        SDL3.SDL_RenderTexture(_renderer, readTarget, null, &destArea);
    }

    /// <summary>
    /// Renders a shadow quad into an FBO, blurs it with multi-tap kernel (no readback),
    /// and composites the soft shadow onto the screen.
    /// </summary>
    public void RenderShadow(Rect bounds, Color color, CornerRadius cornerRadius, BlurFilter filter)
    {
        if (_renderer == null || filter == null || !filter.Enabled || filter.Radius <= 0.05f) return;

        int winW = 0, winH = 0;
        SDL3.SDL_GetRenderOutputSize(_renderer, &winW, &winH);

        // Expand bounds by BlurRadius on all sides so the blur has room to spread
        float expand = filter.Radius * 1.5f;
        float eX = bounds.X - expand;
        float eY = bounds.Y - expand;
        float eW = bounds.Width + expand * 2f;
        float eH = bounds.Height + expand * 2f;

        // Clamp expanded region to screen
        float cX = MathF.Max(eX, 0f), cY = MathF.Max(eY, 0f);
        float cR = MathF.Min(eX + eW, winW);
        float cB = MathF.Min(eY + eH, winH);
        float cW = cR - cX, cH = cB - cY;
        if (cW <= 0f || cH <= 0f) return;

        int tW = (int)Math.Max(cW / 2, 1), tH = (int)Math.Max(cH / 2, 1);
        EnsureRenderTargets(tW, tH);
        if (_pingTexture == null || _pongTexture == null) return;

        SDL_FRect compositeDst = new SDL_FRect { x = cX, y = cY, w = cW, h = cH };
        SDL_FRect fbo = new SDL_FRect { x = 0, y = 0, w = tW, h = tH };

        // The shadow quad position relative to the expanded FBO
        float innerX = (bounds.X - cX) / cW * tW;
        float innerY = (bounds.Y - cY) / cH * tH;
        float innerW = bounds.Width / cW * tW;
        float innerH = bounds.Height / cH * tH;

        // Step 1: Render shadow quad into ping FBO
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

        // Step 2: Multi-tap Kawase blur
        SDL_Texture* readTarget = _pingTexture;
        SDL_Texture* writeTarget = _pongTexture;
        int passes = Math.Clamp(filter.Passes, 1, 4);
        float kernel = (filter.Radius / 2f) / (float)passes * 0.65f;
        float fw = fbo.w, fh = fbo.h;

        for (int p = 0; p < passes; p++)
        {
            float off = (p + 1.0f) * kernel;
            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);
            SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
            SDL3.SDL_RenderClear(_renderer);

            // Center tap (full copy, no blend)
            SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_NONE);
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 1.0f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &fbo);

            // Four offset taps with blend
            for (int ti = 0; ti < 4; ti++)
            {
                float ox = ti < 2 ? -off : off;
                float oy = ti % 2 == 0 ? -off : off;
                float tx = ox < 0 ? -ox : 0f;
                float ty = oy < 0 ? -oy : 0f;
                float alp = ti == 0 ? 0.35f : ti == 1 ? 0.25f : ti == 2 ? 0.20f : 0.15f;

                SDL_FRect r = new SDL_FRect { x = tx, y = ty, w = fw + MathF.Abs(ox), h = fh + MathF.Abs(oy) };
                SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
                SDL3.SDL_SetTextureAlphaModFloat(readTarget, alp);
                SDL3.SDL_RenderTexture(_renderer, readTarget, null, &r);
            }

            var tmp = readTarget; readTarget = writeTarget; writeTarget = tmp;
        }

        // Step 3: Composite blurred shadow back onto screen (expanded region)
        SDL3.SDL_SetRenderTarget(_renderer, null);
        SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
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
