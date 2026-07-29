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
    public void RenderText(Font? font, float fontSize, string text, Rect bounds, Color color)
    {
        font ??= FontEngine.DefaultFont;
        if (!IsInitialized || _renderer == null || font == null || string.IsNullOrEmpty(text) || color.A <= 0.001f) return;

        TTF_Font* fontHandle = font.GetHandle(fontSize);
        if (fontHandle == null) return;

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
            SDL_Surface* surface = SDL3_ttf.TTF_RenderText_Blended(fontHandle, tPtr, (nuint)text.Length, fgColor);
            if (surface == null) return;

            SDL_Texture* texture = SDL3.SDL_CreateTextureFromSurface(_renderer, surface);
            float textW = surface->w;
            float textH = surface->h;
            SDL3.SDL_DestroySurface(surface);

            if (texture != null)
            {
                SDL3.SDL_SetTextureBlendMode(texture, SDL_BlendMode.SDL_BLENDMODE_BLEND);
                SDL3.SDL_SetTextureAlphaModFloat(texture, Math.Clamp(color.A, 0f, 1f));

                // Center text inside bounds (round to pixel grid to prevent blur)
                float posX = MathF.Floor(bounds.X + (bounds.Width - textW) * 0.5f);
                float posY = MathF.Floor(bounds.Y + (bounds.Height - textH) * 0.5f);

                SDL_FRect destRect = new SDL_FRect { x = posX, y = posY, w = textW, h = textH };
                SDL3.SDL_RenderTexture(_renderer, texture, null, &destRect);
                SDL3.SDL_DestroyTexture(texture);
            }
        }
    }

    public void RenderText(Font? font, string text, Rect bounds, Color color)
    {
        RenderText(font, font?.DefaultSize ?? 16.0f, text, bounds, color);
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
                            RenderText(cmd.Font, cmd.FontSize, cmd.TextString, cmd.Bounds, cmd.Color);
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
                case 6: // Stroke (outline border)
                    {
                        RenderStroke(cmd.Bounds, cmd.Stroke, cmd.CornerRadius);
                        break;
                    }
            }
        }

        // 3. Swap buffers (Present frame to window display)
        SDL3.SDL_RenderPresent(_renderer);
    }

    /// <summary>
    /// Draws a filled rounded rectangle with independent per-corner rounding and 1.0px Sub-Pixel AA Fringe Ring for crisp, resolution-independent anti-aliasing.
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

        // 4. Render 4 corner fans with 1.0px Sub-Pixel AA Fringe Ring
        float maxSegR = MathF.Max(MathF.Max(rTL, rTR), MathF.Max(rBL, rBR));
        int segments = Math.Clamp((int)(maxSegR * 0.75f) + 8, 12, 32);

        float cr = color.R, cg = color.G, cb = color.B, ca = color.A;
        SDL_FColor solidColor = new SDL_FColor { r = cr, g = cg, b = cb, a = ca };
        SDL_FColor transparentColor = new SDL_FColor { r = cr, g = cg, b = cb, a = 0f };

        var corners = new[] {
            (x + rTL,         y + rTL,         rTL, 180f, 270f), // Top-Left
            (x + w - rTR,     y + rTR,         rTR, 270f, 360f), // Top-Right
            (x + w - rBR,     y + h - rBR,     rBR, 0f,   90f),  // Bottom-Right
            (x + rBL,         y + h - rBL,     rBL, 90f,  180f), // Bottom-Left
        };

        // Each corner fan has: 1 center vert + (segments + 1) inner verts (solid) + (segments + 1) outer verts (AA transparent)
        int vertsPerCorner = 1 + (segments + 1) * 2;
        int totalVerts = 4 * vertsPerCorner;
        var verts = new SDL_Vertex[totalVerts];

        // Indices: 3 * segments for inner solid fan + 6 * segments for outer AA fringe quad ring
        int indicesPerCorner = 3 * segments + 6 * segments;
        int[] indices = new int[4 * indicesPerCorner];

        int vIdx = 0;
        int iIdx = 0;

        foreach (var (cx, cy, r, startAngle, endAngle) in corners)
        {
            int cornerBaseVert = vIdx;

            if (r <= 1f)
            {
                // Square corner fallback
                for (int i = 0; i < vertsPerCorner; i++)
                {
                    verts[vIdx++] = new SDL_Vertex { position = new SDL_FPoint { x = cx, y = cy }, color = solidColor };
                }
                continue;
            }

            float rInner = MathF.Max(0.1f, r - 0.5f);
            float rOuter = r + 0.5f;

            // Center vertex
            verts[vIdx++] = new SDL_Vertex { position = new SDL_FPoint { x = cx, y = cy }, color = solidColor };

            int innerStartIdx = vIdx;
            // Inner solid arc
            for (int i = 0; i <= segments; i++)
            {
                float angle = (startAngle + (endAngle - startAngle) * i / segments) * MathF.PI / 180f;
                verts[vIdx++] = new SDL_Vertex
                {
                    position = new SDL_FPoint { x = cx + rInner * MathF.Cos(angle), y = cy + rInner * MathF.Sin(angle) },
                    color = solidColor
                };
            }

            int outerStartIdx = vIdx;
            // Outer AA transparent fringe arc
            for (int i = 0; i <= segments; i++)
            {
                float angle = (startAngle + (endAngle - startAngle) * i / segments) * MathF.PI / 180f;
                verts[vIdx++] = new SDL_Vertex
                {
                    position = new SDL_FPoint { x = cx + rOuter * MathF.Cos(angle), y = cy + rOuter * MathF.Sin(angle) },
                    color = transparentColor
                };
            }

            // Build inner solid fan indices
            for (int i = 0; i < segments; i++)
            {
                indices[iIdx++] = cornerBaseVert;
                indices[iIdx++] = innerStartIdx + i;
                indices[iIdx++] = innerStartIdx + i + 1;
            }

            // Build outer AA fringe ring quad indices (2 triangles per segment)
            for (int i = 0; i < segments; i++)
            {
                int in1 = innerStartIdx + i;
                int in2 = innerStartIdx + i + 1;
                int out1 = outerStartIdx + i;
                int out2 = outerStartIdx + i + 1;

                indices[iIdx++] = in1;
                indices[iIdx++] = out1;
                indices[iIdx++] = in2;

                indices[iIdx++] = in2;
                indices[iIdx++] = out1;
                indices[iIdx++] = out2;
            }
        }

        fixed (SDL_Vertex* vPtr = verts)
        fixed (int* iPtr = indices)
        {
            SDL3.SDL_RenderGeometry(_renderer, null, vPtr, totalVerts, iPtr, iIdx);
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

        float maxSegR = MathF.Max(MathF.Max(rTL, rTR), MathF.Max(rBL, rBR));
        int segments = maxSegR <= 1f ? 0 : Math.Clamp((int)(maxSegR / 3f) + 6, 6, 24);

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

        // ── Reusable vertex pool for stroke geometry ───────────────────────────────
    private SDL_Vertex[] _strokeVerts = new SDL_Vertex[256];
    private int[] _strokeIndices = new int[512];

    /// <summary>
    /// Draws a rounded-outline stroke ring using SDL_RenderGeometry with 1.0px Sub-Pixel Alpha Fringe Ring on inner & outer edges for razor-sharp AA borders.
    /// </summary>
    private void RenderStroke(Rect bounds, StrokeInfo stroke, CornerRadius cornerRadius)
    {
        float t = stroke.Thickness;
        Color c = stroke.Color;
        if (t <= 0f || c.A <= 0f) return;

        float bx = bounds.X, by = bounds.Y, bw = bounds.Width, bh = bounds.Height;
        float maxR = MathF.Min(bw, bh) * 0.5f;

        float crTL = MathF.Min(cornerRadius.TopLeft, maxR);
        float crTR = MathF.Min(cornerRadius.TopRight, maxR);
        float crBL = MathF.Min(cornerRadius.BottomLeft, maxR);
        float crBR = MathF.Min(cornerRadius.BottomRight, maxR);

        // Determine base outer and inner bounding rectangles & corner radii
        float outX, outY, outW, outH;
        float inX, inY, inW, inH;
        float outTL, outTR, outBR, outBL;
        float inTL, inTR, inBR, inBL;

        switch (stroke.Alignment)
        {
            case StrokeAlignment.Outside:
                outX = bx - t; outY = by - t; outW = bw + t * 2f; outH = bh + t * 2f;
                inX = bx; inY = by; inW = bw; inH = bh;

                outTL = crTL + t; outTR = crTR + t; outBR = crBR + t; outBL = crBL + t;
                inTL = crTL; inTR = crTR; inBR = crBR; inBL = crBL;
                break;

            case StrokeAlignment.Center:
                float hT = t * 0.5f;
                outX = bx - hT; outY = by - hT; outW = bw + t; outH = bh + t;
                inX = bx + hT; inY = by + hT; inW = MathF.Max(0f, bw - t); inH = MathF.Max(0f, bh - t);

                outTL = crTL + hT; outTR = crTR + hT; outBR = crBR + hT; outBL = crBL + hT;
                inTL = MathF.Max(0f, crTL - hT); inTR = MathF.Max(0f, crTR - hT);
                inBR = MathF.Max(0f, crBR - hT); inBL = MathF.Max(0f, crBL - hT);
                break;

            default: // Inside
                outX = bx; outY = by; outW = bw; outH = bh;
                inX = bx + t; inY = by + t; inW = MathF.Max(0f, bw - t * 2f); inH = MathF.Max(0f, bh - t * 2f);

                outTL = crTL; outTR = crTR; outBR = crBR; outBL = crBL;
                inTL = MathF.Max(0f, crTL - t); inTR = MathF.Max(0f, crTR - t);
                inBR = MathF.Max(0f, crBR - t); inBL = MathF.Max(0f, crBL - t);
                break;
        }

        // Adaptive segment count
        static int Segs(float r) => Math.Clamp((int)(r * 0.75f) + 8, 12, 32);

        int sTL = Segs(outTL), sTR = Segs(outTR), sBR = Segs(outBR), sBL = Segs(outBL);
        int contourCount = (sTL + 1) + (sTR + 1) + (sBR + 1) + (sBL + 1);

        // 4 contours: Outer AA (transparent), Outer Solid, Inner Solid, Inner AA (transparent)
        int totalVerts = contourCount * 4;
        int totalQuads = contourCount * 3; // 3 quad rings of contourCount quads each
        int totalIndices = totalQuads * 6;

        if (_strokeVerts.Length < totalVerts || _strokeIndices.Length < totalIndices)
        {
            _strokeVerts = new SDL_Vertex[Math.Max(totalVerts, _strokeVerts.Length * 2)];
            _strokeIndices = new int[Math.Max(totalIndices, _strokeIndices.Length * 2)];
        }

        SDL_FColor solidC = new SDL_FColor { r = c.R, g = c.G, b = c.B, a = c.A };
        SDL_FColor transC = new SDL_FColor { r = c.R, g = c.G, b = c.B, a = 0f };

        int vi = 0;

        void EmitContour(float rx, float ry, float rw, float rh, float rTL, float rTR, float rBR, float rBL, float delta, SDL_FColor color)
        {
            float x = rx - delta, y = ry - delta, w = rw + delta * 2f, h = rh + delta * 2f;
            float cTL = MathF.Max(0f, rTL + delta);
            float cTR = MathF.Max(0f, rTR + delta);
            float cBR = MathF.Max(0f, rBR + delta);
            float cBL = MathF.Max(0f, rBL + delta);

            float cxTL = x + cTL, cyTL = y + cTL;
            float cxTR = x + w - cTR, cyTR = y + cTR;
            float cxBR = x + w - cBR, cyBR = y + h - cBR;
            float cxBL = x + cBL, cyBL = y + h - cBL;

            // Top-Left corner
            for (int i = 0; i <= sTL; i++)
            {
                float a = (180f + 90f * i / sTL) * MathF.PI / 180f;
                _strokeVerts[vi++] = new SDL_Vertex { position = new SDL_FPoint { x = cxTL + cTL * MathF.Cos(a), y = cyTL + cTL * MathF.Sin(a) }, color = color };
            }
            // Top-Right corner
            for (int i = 0; i <= sTR; i++)
            {
                float a = (270f + 90f * i / sTR) * MathF.PI / 180f;
                _strokeVerts[vi++] = new SDL_Vertex { position = new SDL_FPoint { x = cxTR + cTR * MathF.Cos(a), y = cyTR + cTR * MathF.Sin(a) }, color = color };
            }
            // Bottom-Right corner
            for (int i = 0; i <= sBR; i++)
            {
                float a = (0f + 90f * i / sBR) * MathF.PI / 180f;
                _strokeVerts[vi++] = new SDL_Vertex { position = new SDL_FPoint { x = cxBR + cBR * MathF.Cos(a), y = cyBR + cBR * MathF.Sin(a) }, color = color };
            }
            // Bottom-Left corner
            for (int i = 0; i <= sBL; i++)
            {
                float a = (90f + 90f * i / sBL) * MathF.PI / 180f;
                _strokeVerts[vi++] = new SDL_Vertex { position = new SDL_FPoint { x = cxBL + cBL * MathF.Cos(a), y = cyBL + cBL * MathF.Sin(a) }, color = color };
            }
        }

        // Contour 0: Outer AA Transparent Fringe (+0.5px)
        EmitContour(outX, outY, outW, outH, outTL, outTR, outBR, outBL, 0.5f, transC);
        // Contour 1: Outer Solid Boundary (-0.5px)
        EmitContour(outX, outY, outW, outH, outTL, outTR, outBR, outBL, -0.5f, solidC);
        // Contour 2: Inner Solid Boundary (+0.5px)
        EmitContour(inX, inY, inW, inH, inTL, inTR, inBR, inBL, 0.5f, solidC);
        // Contour 3: Inner AA Transparent Fringe (-0.5px)
        EmitContour(inX, inY, inW, inH, inTL, inTR, inBR, inBL, -0.5f, transC);

        // Build quad indices connecting the 4 contours
        int ii = 0;
        int cLen = contourCount;

        void BuildRing(int ringA, int ringB)
        {
            for (int i = 0; i < cLen; i++)
            {
                int next = (i + 1) % cLen;
                int a1 = ringA + i, a2 = ringA + next;
                int b1 = ringB + i, b2 = ringB + next;

                _strokeIndices[ii++] = a1; _strokeIndices[ii++] = b1; _strokeIndices[ii++] = a2;
                _strokeIndices[ii++] = a2; _strokeIndices[ii++] = b1; _strokeIndices[ii++] = b2;
            }
        }

        // Ring 1: Outer AA (0 -> 1)
        BuildRing(0 * cLen, 1 * cLen);
        // Ring 2: Solid Stroke Body (1 -> 2)
        BuildRing(1 * cLen, 2 * cLen);
        // Ring 3: Inner AA (2 -> 3)
        BuildRing(2 * cLen, 3 * cLen);

        fixed (SDL_Vertex* vPtr = _strokeVerts)
        fixed (int* iPtr = _strokeIndices)
        {
            SDL3.SDL_RenderGeometry(_renderer, null, vPtr, totalVerts, iPtr, ii);
        }
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
