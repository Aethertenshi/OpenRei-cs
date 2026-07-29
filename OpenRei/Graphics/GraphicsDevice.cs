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
    /// Renders text using SDL3_ttf blended font surface with alpha blending support.
    /// </summary>
    public void RenderText(Font? font, string text, Rect bounds, Color color)
    {
        font ??= FontEngine.DefaultFont;
        if (!IsInitialized || _renderer == null || font == null || font.Handle == null || string.IsNullOrEmpty(text) || color.A <= 0.001f) return;

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
                SDL3.SDL_SetTextureBlendMode(texture, SDL_BlendMode.SDL_BLENDMODE_BLEND);
                SDL3.SDL_SetTextureAlphaModFloat(texture, Math.Clamp(color.A, 0f, 1f));

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
        SDL3.SDL_SetTextureColorModFloat(texture.Handle, Math.Clamp(color.R, 0f, 1f), Math.Clamp(color.G, 0f, 1f), Math.Clamp(color.B, 0f, 1f));
        SDL3.SDL_SetTextureAlphaModFloat(texture.Handle, Math.Clamp(color.A, 0f, 1f));

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

        // Attach renderer handle for inline clip rect operations
        context.RendererHandle = _renderer;

        // 1. Clear background to dark theme color
        SDL3.SDL_SetRenderDrawColor(_renderer, 18, 18, 24, 255);
        SDL3.SDL_RenderClear(_renderer);

        // 2. Process unified sorted commands (Quads, Images, Text, ClipPush, ClipPop)
        foreach (var cmd in context.Commands)
        {
            switch (cmd.Type)
            {
                case 0: // Quad
                    {
                        var r = cmd.CornerRadius;
                        if (r.TopLeft > 1.0f || r.TopRight > 1.0f || r.BottomLeft > 1.0f || r.BottomRight > 1.0f)
                        {
                            RenderRoundedRect(cmd.Bounds, cmd.Color, cmd.CornerRadius);
                        }
                        else
                        {
                            SDL_FRect rect = new SDL_FRect { x = cmd.Bounds.X, y = cmd.Bounds.Y, w = cmd.Bounds.Width, h = cmd.Bounds.Height };
                            SDL3.SDL_SetRenderDrawColorFloat(_renderer, cmd.Color.R, cmd.Color.G, cmd.Color.B, cmd.Color.A);
                            SDL3.SDL_RenderFillRect(_renderer, &rect);
                        }
                        break;
                    }
                case 1: // Image
                    {
                        if (cmd.BlurFilter != null && cmd.BlurFilter.Enabled && cmd.BlurFilter.Radius > 0.05f)
                            _blurPipeline?.RenderBlurredTexture(cmd.Texture!, cmd.Bounds, cmd.SourceRect, cmd.Color, cmd.BlurFilter);
                        else if (cmd.CornerRadius.TopLeft > 1f || cmd.CornerRadius.TopRight > 1f || cmd.CornerRadius.BottomLeft > 1f || cmd.CornerRadius.BottomRight > 1f)
                            RenderRoundedImage(cmd.Texture!, cmd.Bounds, cmd.SourceRect, cmd.Color, cmd.CornerRadius);
                        else
                            RenderImage(cmd.Texture!, cmd.Bounds, cmd.SourceRect, cmd.Color);
                        break;
                    }
                case 2: // Text
                    {
                        if (cmd.TextString != null)
                            RenderText(cmd.Font, cmd.TextString, cmd.Bounds, cmd.Color);
                        break;
                    }
                case 3: // ClipPush
                    {
                        SDL_Rect r = new SDL_Rect { x = (int)cmd.Bounds.X, y = (int)cmd.Bounds.Y, w = (int)cmd.Bounds.Width, h = (int)cmd.Bounds.Height };
                        SDL3.SDL_SetRenderClipRect(_renderer, &r);
                        break;
                    }
                case 4: // ClipPop
                    {
                        SDL3.SDL_SetRenderClipRect(_renderer, null);
                        break;
                    }
                case 5: // BlurRegion (FBO-only shadow blur, no readback)
                    {
                        _blurPipeline?.RenderShadow(cmd.Bounds, cmd.Color, cmd.CornerRadius, cmd.BlurFilter!);
                        break;
                    }
            }
        }

        // 3. Swap buffers (Present frame to window display)
        SDL3.SDL_RenderPresent(_renderer);
    }

    /// <summary>
    /// Draws a filled rounded rectangle with independent per-corner rounding (TopLeft, TopRight, BottomLeft, BottomRight).
    /// </summary>
    private void RenderRoundedRect(Rect bounds, Color color, CornerRadius radius)
    {
        float x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height;
        float maxR = MathF.Min(w, h) * 0.5f;

        float rTL = MathF.Min(radius.TopLeft, maxR);
        float rTR = MathF.Min(radius.TopRight, maxR);
        float rBL = MathF.Min(radius.BottomLeft, maxR);
        float rBR = MathF.Min(radius.BottomRight, maxR);

        if (rTL <= 1f && rTR <= 1f && rBL <= 1f && rBR <= 1f)
        {
            SDL_FRect fallback = new SDL_FRect { x = x, y = y, w = w, h = h };
            SDL3.SDL_SetRenderDrawColorFloat(_renderer, color.R, color.G, color.B, color.A);
            SDL3.SDL_RenderFillRect(_renderer, &fallback);
            return;
        }

        SDL3.SDL_SetRenderDrawColorFloat(_renderer, color.R, color.G, color.B, color.A);

        float maxTopR = MathF.Max(rTL, rTR);
        float maxBotR = MathF.Max(rBL, rBR);

        // 1. Center-middle rect (Full width, inset top by maxTopR & bottom by maxBotR)
        SDL_FRect midRect = new SDL_FRect { x = x, y = y + maxTopR, w = w, h = MathF.Max(0f, h - maxTopR - maxBotR) };
        if (midRect.h > 0f) SDL3.SDL_RenderFillRect(_renderer, &midRect);

        // 2. Top-center rect (Inset left by rTL & right by rTR)
        SDL_FRect topRect = new SDL_FRect { x = x + rTL, y = y, w = MathF.Max(0f, w - rTL - rTR), h = maxTopR };
        if (topRect.w > 0f && topRect.h > 0f) SDL3.SDL_RenderFillRect(_renderer, &topRect);

        // 3. Bottom-center rect (Inset left by rBL & right by rBR)
        SDL_FRect botRect = new SDL_FRect { x = x + rBL, y = y + h - maxBotR, w = MathF.Max(0f, w - rBL - rBR), h = maxBotR };
        if (botRect.w > 0f && botRect.h > 0f) SDL3.SDL_RenderFillRect(_renderer, &botRect);

        // 4. Render 4 corner fans (or degenerate fallback if r == 0)
        int segments = 8;
        int totalVerts = 4 * (segments + 2);
        var verts = new SDL_Vertex[totalVerts];
        int idx = 0;

        float cr = color.R, cg = color.G, cb = color.B, ca = color.A;

        var corners = new[] {
            (x + rTL,         y + rTL,         rTL, 180f, 270f), // Top-Left
            (x + w - rTR,     y + rTR,         rTR, 270f, 360f), // Top-Right
            (x + w - rBR,     y + h - rBR,     rBR, 0f,   90f),  // Bottom-Right
            (x + rBL,         y + h - rBL,     rBL, 90f,  180f), // Bottom-Left
        };

        foreach (var (cx, cy, r, startAngle, endAngle) in corners)
        {
            if (r <= 1f)
            {
                // Square corner fallback
                for (int i = 0; i <= segments + 1; i++)
                {
                    verts[idx++] = new SDL_Vertex
                    {
                        position = new SDL_FPoint { x = cx, y = cy },
                        color = new SDL_FColor { r = cr, g = cg, b = cb, a = ca }
                    };
                }
                continue;
            }

            // Fan center
            verts[idx++] = new SDL_Vertex
            {
                position = new SDL_FPoint { x = cx, y = cy },
                color = new SDL_FColor { r = cr, g = cg, b = cb, a = ca }
            };
            for (int i = 0; i <= segments; i++)
            {
                float angle = (startAngle + (endAngle - startAngle) * i / segments) * MathF.PI / 180f;
                verts[idx++] = new SDL_Vertex
                {
                    position = new SDL_FPoint { x = cx + r * MathF.Cos(angle), y = cy + r * MathF.Sin(angle) },
                    color = new SDL_FColor { r = cr, g = cg, b = cb, a = ca }
                };
            }
        }

        fixed (SDL_Vertex* vPtr = verts)
        {
            int[] indices = new int[4 * (3 * segments)];
            int iIdx = 0;
            int cornerPos = 0;
            for (int c = 0; c < 4; c++)
            {
                int fanCenter = cornerPos;
                for (int s = 0; s < segments; s++)
                {
                    indices[iIdx++] = fanCenter;
                    indices[iIdx++] = fanCenter + 1 + s;
                    indices[iIdx++] = fanCenter + 2 + s;
                }
                cornerPos += (segments + 2);
            }

            fixed (int* iPtr = indices)
            {
                SDL3.SDL_RenderGeometry(_renderer, null, vPtr, totalVerts, iPtr, iIdx);
            }
        }
    }

    /// <summary>
    /// Renders a texture with per-corner rounding by drawing UV-mapped vertices through SDL_RenderGeometry.
    /// The texture is rendered into an FBO first, then composited via rounded geometry to avoid UV complexity.
    /// </summary>
    private void RenderRoundedImage(Texture texture, Rect destBounds, Rect? sourceRect, Color color, CornerRadius radius)
    {
        if (!IsInitialized || _renderer == null || texture == null || !texture.IsValid) return;

        int tw = (int)MathF.Max(destBounds.Width, 1f);
        int th = (int)MathF.Max(destBounds.Height, 1f);

        // Create a temporary render target to draw the image into
        SDL_Texture* fbo = SDL3.SDL_CreateTexture(_renderer,
            SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888,
            SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET,
            tw, th);
        if (fbo == null) { RenderImage(texture, destBounds, sourceRect, color); return; }

        // Step 1: Render image into FBO
        SDL3.SDL_SetRenderTarget(_renderer, fbo);
        SDL3.SDL_SetRenderDrawColorFloat(_renderer, 0, 0, 0, 0);
        SDL3.SDL_RenderClear(_renderer);

        SDL3.SDL_SetTextureBlendMode(texture.Handle, SDL_BlendMode.SDL_BLENDMODE_BLEND);
        SDL3.SDL_SetTextureColorModFloat(texture.Handle, Math.Clamp(color.R, 0f, 1f), Math.Clamp(color.G, 0f, 1f), Math.Clamp(color.B, 0f, 1f));
        SDL3.SDL_SetTextureAlphaModFloat(texture.Handle, Math.Clamp(color.A, 0f, 1f));

        SDL_FRect fboDest = new SDL_FRect { x = 0, y = 0, w = tw, h = th };
        if (sourceRect.HasValue)
        {
            SDL_FRect src = new SDL_FRect { x = sourceRect.Value.X, y = sourceRect.Value.Y, w = sourceRect.Value.Width, h = sourceRect.Value.Height };
            SDL3.SDL_RenderTexture(_renderer, texture.Handle, &src, &fboDest);
        }
        else
        {
            SDL3.SDL_RenderTexture(_renderer, texture.Handle, null, &fboDest);
        }

        // Step 2: Switch back to screen and composite through rounded geometry
        SDL3.SDL_SetRenderTarget(_renderer, null);
        SDL3.SDL_SetTextureBlendMode(fbo, SDL_BlendMode.SDL_BLENDMODE_BLEND);

        float x = destBounds.X, y = destBounds.Y, w = destBounds.Width, h = destBounds.Height;
        float maxR = MathF.Min(w, h) * 0.5f;
        float rTL = MathF.Min(radius.TopLeft, maxR);
        float rTR = MathF.Min(radius.TopRight, maxR);
        float rBL = MathF.Min(radius.BottomLeft, maxR);
        float rBR = MathF.Min(radius.BottomRight, maxR);

        int segments = 8;

        // Build UV-mapped vertices for 3 fill rects (6 verts each = 18) + 4 corner fans (segments+2 each)
        int rectVerts = 18;
        int fanVerts = 4 * (segments + 2);
        int totalVerts = rectVerts + fanVerts;
        var verts = new SDL_Vertex[totalVerts];
        int vi = 0;

        float maxTopR = MathF.Max(rTL, rTR);
        float maxBotR = MathF.Max(rBL, rBR);

        SDL_FColor white = new SDL_FColor { r = 1f, g = 1f, b = 1f, a = 1f };

        // Helper: screen pos -> UV
        float uvX(float px) => (px - x) / w;
        float uvY(float py) => (py - y) / h;

        void AddQuadVert(float px, float py)
        {
            verts[vi++] = new SDL_Vertex
            {
                position = new SDL_FPoint { x = px, y = py },
                color = white,
                tex_coord = new SDL_FPoint { x = uvX(px), y = uvY(py) }
            };
        }

        // Middle rect (2 triangles = 6 verts)
        float mT = y + maxTopR, mB = y + h - maxBotR;
        AddQuadVert(x, mT); AddQuadVert(x + w, mT); AddQuadVert(x + w, mB);
        AddQuadVert(x, mT); AddQuadVert(x + w, mB); AddQuadVert(x, mB);

        // Top-center rect
        float tL = x + rTL, tR = x + w - rTR;
        AddQuadVert(tL, y); AddQuadVert(tR, y); AddQuadVert(tR, y + maxTopR);
        AddQuadVert(tL, y); AddQuadVert(tR, y + maxTopR); AddQuadVert(tL, y + maxTopR);

        // Bottom-center rect
        float bL = x + rBL, bR = x + w - rBR, bT = y + h - maxBotR;
        AddQuadVert(bL, bT); AddQuadVert(bR, bT); AddQuadVert(bR, y + h);
        AddQuadVert(bL, bT); AddQuadVert(bR, y + h); AddQuadVert(bL, y + h);

        // 4 corner fans
        var corners = new[] {
            (x + rTL,         y + rTL,         rTL, 180f, 270f),
            (x + w - rTR,     y + rTR,         rTR, 270f, 360f),
            (x + w - rBR,     y + h - rBR,     rBR, 0f,   90f),
            (x + rBL,         y + h - rBL,     rBL, 90f,  180f),
        };

        int fanStartIdx = vi;
        foreach (var (cx, cy, r, startAngle, endAngle) in corners)
        {
            float cr = r <= 1f ? 0f : r;
            // Fan center
            verts[vi++] = new SDL_Vertex
            {
                position = new SDL_FPoint { x = cx, y = cy },
                color = white,
                tex_coord = new SDL_FPoint { x = uvX(cx), y = uvY(cy) }
            };
            for (int i = 0; i <= segments; i++)
            {
                float angle = (startAngle + (endAngle - startAngle) * i / segments) * MathF.PI / 180f;
                float px = cx + cr * MathF.Cos(angle);
                float py = cy + cr * MathF.Sin(angle);
                verts[vi++] = new SDL_Vertex
                {
                    position = new SDL_FPoint { x = px, y = py },
                    color = white,
                    tex_coord = new SDL_FPoint { x = uvX(px), y = uvY(py) }
                };
            }
        }

        // Build index buffer: 18 rect verts (drawn as triangles directly) + 4 corner fans
        int rectIndices = 18;
        int fanIndices = 4 * 3 * segments;
        int[] indices = new int[rectIndices + fanIndices];
        int ii = 0;

        // Rect indices (already in triangle order)
        for (int i = 0; i < 18; i++) indices[ii++] = i;

        // Fan indices
        int cornerPos = fanStartIdx;
        for (int c = 0; c < 4; c++)
        {
            int fanCenter = cornerPos;
            for (int s = 0; s < segments; s++)
            {
                indices[ii++] = fanCenter;
                indices[ii++] = fanCenter + 1 + s;
                indices[ii++] = fanCenter + 2 + s;
            }
            cornerPos += (segments + 2);
        }

        fixed (SDL_Vertex* vPtr = verts)
        fixed (int* iPtr = indices)
        {
            SDL3.SDL_RenderGeometry(_renderer, fbo, vPtr, totalVerts, iPtr, ii);
        }

        SDL3.SDL_DestroyTexture(fbo);
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
