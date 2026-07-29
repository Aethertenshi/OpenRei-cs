using OpenRei.Core;
using OpenRei.Filters;
using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Executes Hardware-Accelerated Multi-Pass Gaussian Blur using weighted additive accumulation and bilinear filtering.
/// </summary>
public unsafe class BlurPipeline : IDisposable
{
    private SDL_Renderer* _renderer;
    private SDL_Texture* _pingTexture;
    private SDL_Texture* _pongTexture;
    private int _currentW;
    private int _currentH;
    private bool _isDisposed;

    private static SDL_BlendMode _additiveBlendMode;
    private static bool _blendModeInitialized;

    public BlurPipeline(SDL_Renderer* renderer)
    {
        _renderer = renderer;
        EnsureBlendMode();
    }

    private static void EnsureBlendMode()
    {
        if (!_blendModeInitialized)
        {
            _additiveBlendMode = SDL3.SDL_ComposeCustomBlendMode(
                SDL_BlendFactor.SDL_BLENDFACTOR_SRC_ALPHA, SDL_BlendFactor.SDL_BLENDFACTOR_ONE, SDL_BlendOperation.SDL_BLENDOPERATION_ADD,
                SDL_BlendFactor.SDL_BLENDFACTOR_SRC_ALPHA, SDL_BlendFactor.SDL_BLENDFACTOR_ONE, SDL_BlendOperation.SDL_BLENDOPERATION_ADD
            );
            _blendModeInitialized = true;
        }
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
    /// Renders a texture directly onto the screen with Multi-Pass Gaussian Blur applied with true additive weighted summation.
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

        // Step 1: Render source texture into Ping FBO
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

        // Step 2: Multi-Pass True Weighted Additive Gaussian Blur
        SDL_Texture* readTarget = _pingTexture;
        SDL_Texture* writeTarget = _pongTexture;

        float step = (filter.Radius / (float)downscale) / (float)passes * 0.8f;

        for (int p = 0; p < passes; p++)
        {
            float offset = (p + 1.0f) * step;

            // --- Horizontal 1D Additive Pass ---
            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);
            SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
            SDL3.SDL_RenderClear(_renderer);
            SDL3.SDL_SetTextureBlendMode(readTarget, _additiveBlendMode);

            // Center (weight 0.383)
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.383f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &fboDest);

            // Left (-offset, weight 0.308)
            SDL_FRect hLeft = new SDL_FRect { x = -offset, y = 0, w = targetW, h = targetH };
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &hLeft);

            // Right (+offset, weight 0.308)
            SDL_FRect hRight = new SDL_FRect { x = offset, y = 0, w = targetW, h = targetH };
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &hRight);

            var tmpH = readTarget; readTarget = writeTarget; writeTarget = tmpH;

            // --- Vertical 1D Additive Pass ---
            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);
            SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
            SDL3.SDL_RenderClear(_renderer);
            SDL3.SDL_SetTextureBlendMode(readTarget, _additiveBlendMode);

            // Center (weight 0.383)
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.383f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &fboDest);

            // Top (-offset, weight 0.308)
            SDL_FRect vTop = new SDL_FRect { x = 0, y = -offset, w = targetW, h = targetH };
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &vTop);

            // Bottom (+offset, weight 0.308)
            SDL_FRect vBot = new SDL_FRect { x = 0, y = offset, w = targetW, h = targetH };
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &vBot);

            var tmpV = readTarget; readTarget = writeTarget; writeTarget = tmpV;
        }

        // Step 3: Composite back onto main screen
        SDL3.SDL_SetRenderTarget(_renderer, null);
        SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
        SDL3.SDL_SetTextureColorModFloat(readTarget, 1f, 1f, 1f);
        SDL3.SDL_SetTextureAlphaModFloat(readTarget, 1f);
        SDL3.SDL_RenderTexture(_renderer, readTarget, null, &destArea);
    }

    /// <summary>
    /// Renders Photoshop-quality smooth Gaussian Drop Shadow respecting cornerRadius silhouette
    /// using true additive weighted summation (SDL_BLENDMODE_ADD).
    /// </summary>
    public void RenderShadow(Rect bounds, Color color, CornerRadius cornerRadius, BlurFilter filter)
    {
        if (_renderer == null || filter == null || !filter.Enabled || filter.Radius <= 0.05f) return;

        int winW = 0, winH = 0;
        SDL3.SDL_GetRenderOutputSize(_renderer, &winW, &winH);

        // Expand bounds by BlurRadius so Gaussian blur spread doesn't clip
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

        int downscale = (filter.Radius > 10.0f) ? 2 : 1;
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

        // Step 1: Clear FBOs and render shadow shape (respecting cornerRadius) into Ping FBO
        SDL3.SDL_SetRenderTarget(_renderer, _pingTexture);
        SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
        SDL3.SDL_RenderClear(_renderer);

        SDL3.SDL_SetRenderTarget(_renderer, _pongTexture);
        SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
        SDL3.SDL_RenderClear(_renderer);

        SDL3.SDL_SetRenderTarget(_renderer, _pingTexture);
        Rect shadowQuadBounds = new Rect(innerX, innerY, innerW, innerH);

        // Render rounded quad silhouette if cornerRadius is present, otherwise fill rect
        if (cornerRadius.TopLeft > 1f || cornerRadius.TopRight > 1f || cornerRadius.BottomLeft > 1f || cornerRadius.BottomRight > 1f)
        {
            CornerRadius scaledCorner = new CornerRadius(
                cornerRadius.TopLeft * scaleFactor,
                cornerRadius.TopRight * scaleFactor,
                cornerRadius.BottomLeft * scaleFactor,
                cornerRadius.BottomRight * scaleFactor
            );
            RenderRoundedShadowQuad(shadowQuadBounds, color, scaledCorner);
        }
        else
        {
            SDL3.SDL_SetRenderDrawColorFloat(_renderer, color.R, color.G, color.B, color.A);
            SDL_FRect innerRect = new SDL_FRect { x = innerX, y = innerY, w = innerW, h = innerH };
            SDL3.SDL_RenderFillRect(_renderer, &innerRect);
        }

        // Step 2: Multi-Pass True Weighted Additive Gaussian Blur
        SDL_Texture* readTarget = _pingTexture;
        SDL_Texture* writeTarget = _pongTexture;

        int passes = Math.Clamp(filter.Passes, 2, 5);
        float step = (filter.Radius / (float)downscale) / (float)passes * 0.9f;

        for (int p = 0; p < passes; p++)
        {
            float offset = (p + 1.0f) * step;

            // --- 1D Horizontal Additive Gaussian Pass ---
            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);
            SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
            SDL3.SDL_RenderClear(_renderer);
            SDL3.SDL_SetTextureBlendMode(readTarget, _additiveBlendMode);

            // Center (weight 0.383)
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.383f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &fboFull);

            // Left (-offset, weight 0.308)
            SDL_FRect hLeft = new SDL_FRect { x = -offset, y = 0, w = tW, h = tH };
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &hLeft);

            // Right (+offset, weight 0.308)
            SDL_FRect hRight = new SDL_FRect { x = offset, y = 0, w = tW, h = tH };
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &hRight);

            var tmpH = readTarget; readTarget = writeTarget; writeTarget = tmpH;

            // --- 1D Vertical Additive Gaussian Pass ---
            SDL3.SDL_SetRenderTarget(_renderer, writeTarget);
            SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
            SDL3.SDL_RenderClear(_renderer);
            SDL3.SDL_SetTextureBlendMode(readTarget, _additiveBlendMode);

            // Center (weight 0.383)
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.383f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &fboFull);

            // Top (-offset, weight 0.308)
            SDL_FRect vTop = new SDL_FRect { x = 0, y = -offset, w = tW, h = tH };
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &vTop);

            // Bottom (+offset, weight 0.308)
            SDL_FRect vBot = new SDL_FRect { x = 0, y = offset, w = tW, h = tH };
            SDL3.SDL_SetTextureAlphaModFloat(readTarget, 0.308f);
            SDL3.SDL_RenderTexture(_renderer, readTarget, null, &vBot);

            var tmpV = readTarget; readTarget = writeTarget; writeTarget = tmpV;
        }

        // Step 3: Composite smooth Gaussian shadow back onto screen with hardware bilinear interpolation
        SDL3.SDL_SetRenderTarget(_renderer, null);
        SDL3.SDL_SetTextureBlendMode(readTarget, SDL_BlendMode.SDL_BLENDMODE_BLEND);
        SDL3.SDL_SetTextureColorModFloat(readTarget, 1f, 1f, 1f);
        SDL3.SDL_SetTextureAlphaModFloat(readTarget, Math.Clamp(color.A, 0f, 1f));
        SDL3.SDL_RenderTexture(_renderer, readTarget, null, &compositeDst);
    }

    /// <summary>
    /// Helper to draw a rounded quad silhouette inside an FBO target for rounded drop shadows.
    /// </summary>
    private void RenderRoundedShadowQuad(Rect bounds, Color color, CornerRadius radius)
    {
        float x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height;
        float maxR = MathF.Min(w, h) * 0.5f;

        float rTL = MathF.Min(radius.TopLeft, maxR);
        float rTR = MathF.Min(radius.TopRight, maxR);
        float rBL = MathF.Min(radius.BottomLeft, maxR);
        float rBR = MathF.Min(radius.BottomRight, maxR);

        SDL3.SDL_SetRenderDrawColorFloat(_renderer, color.R, color.G, color.B, color.A);

        float maxTopR = MathF.Max(rTL, rTR);
        float maxBotR = MathF.Max(rBL, rBR);

        SDL_FRect midRect = new SDL_FRect { x = x, y = y + maxTopR, w = w, h = MathF.Max(0f, h - maxTopR - maxBotR) };
        if (midRect.h > 0f) SDL3.SDL_RenderFillRect(_renderer, &midRect);

        SDL_FRect topRect = new SDL_FRect { x = x + rTL, y = y, w = MathF.Max(0f, w - rTL - rTR), h = maxTopR };
        if (topRect.w > 0f && topRect.h > 0f) SDL3.SDL_RenderFillRect(_renderer, &topRect);

        SDL_FRect botRect = new SDL_FRect { x = x + rBL, y = y + h - maxBotR, w = MathF.Max(0f, w - rBL - rBR), h = maxBotR };
        if (botRect.w > 0f && botRect.h > 0f) SDL3.SDL_RenderFillRect(_renderer, &botRect);

        int segments = 12;
        int vertsPerCorner = 1 + (segments + 1);
        int totalVerts = 4 * vertsPerCorner;
        var verts = new SDL_Vertex[totalVerts];
        int[] indices = new int[4 * (3 * segments)];

        SDL_FColor solidColor = new SDL_FColor { r = color.R, g = color.G, b = color.B, a = color.A };

        var corners = new[] {
            (x + rTL,     y + rTL,     rTL, 180f, 270f),
            (x + w - rTR, y + rTR,     rTR, 270f, 360f),
            (x + w - rBR, y + h - rBR, rBR, 0f,   90f),
            (x + rBL,     y + h - rBL, rBL, 90f,  180f),
        };

        int vIdx = 0, iIdx = 0;
        foreach (var (cx, cy, r, startAngle, endAngle) in corners)
        {
            int baseVert = vIdx;
            verts[vIdx++] = new SDL_Vertex { position = new SDL_FPoint { x = cx, y = cy }, color = solidColor };
            int innerStart = vIdx;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (startAngle + (endAngle - startAngle) * i / segments) * MathF.PI / 180f;
                verts[vIdx++] = new SDL_Vertex
                {
                    position = new SDL_FPoint { x = cx + r * MathF.Cos(angle), y = cy + r * MathF.Sin(angle) },
                    color = solidColor
                };
            }

            for (int i = 0; i < segments; i++)
            {
                indices[iIdx++] = baseVert;
                indices[iIdx++] = innerStart + i;
                indices[iIdx++] = innerStart + i + 1;
            }
        }

        fixed (SDL_Vertex* vPtr = verts)
        fixed (int* iPtr = indices)
        {
            SDL3.SDL_RenderGeometry(_renderer, null, vPtr, totalVerts, iPtr, iIdx);
        }
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
