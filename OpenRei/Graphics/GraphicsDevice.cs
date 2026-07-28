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
                        if (cmd.CornerRadius > 1.0f)
                        {
                            RenderRoundedRect(cmd.Bounds, cmd.Color, cmd.CornerRadius);
                        }
                        else
                        {
                            SDL_FRect r = new SDL_FRect { x = cmd.Bounds.X, y = cmd.Bounds.Y, w = cmd.Bounds.Width, h = cmd.Bounds.Height };
                            SDL3.SDL_SetRenderDrawColorFloat(_renderer, cmd.Color.R, cmd.Color.G, cmd.Color.B, cmd.Color.A);
                            SDL3.SDL_RenderFillRect(_renderer, &r);
                        }
                        break;
                    }
                case 1: // Image
                    {
                        if (cmd.BlurFilter != null && cmd.BlurFilter.Enabled && cmd.BlurFilter.Radius > 0.05f)
                            _blurPipeline?.RenderBlurredTexture(cmd.Texture!, cmd.Bounds, cmd.SourceRect, cmd.Color, cmd.BlurFilter);
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
            }
        }

        // 3. Swap buffers (Present frame to window display)
        SDL3.SDL_RenderPresent(_renderer);
    }

    /// <summary>
    /// Draws a filled rounded rectangle seamlessly using 3 fill rectangles + 4 corner fan geometries.
    /// </summary>
    private void RenderRoundedRect(Rect bounds, Color color, float radius)
    {
        float x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height;
        float r = MathF.Min(radius, MathF.Min(w, h) * 0.5f);
        if (r <= 1f)
        {
            SDL_FRect fallback = new SDL_FRect { x = x, y = y, w = w, h = h };
            SDL3.SDL_SetRenderDrawColorFloat(_renderer, color.R, color.G, color.B, color.A);
            SDL3.SDL_RenderFillRect(_renderer, &fallback);
            return;
        }

        SDL3.SDL_SetRenderDrawColorFloat(_renderer, color.R, color.G, color.B, color.A);

        // 1. Middle rect (Full height between inset top & bottom)
        SDL_FRect midRect = new SDL_FRect { x = x, y = y + r, w = w, h = MathF.Max(0f, h - 2f * r) };
        if (midRect.h > 0f) SDL3.SDL_RenderFillRect(_renderer, &midRect);

        // 2. Top-center rect (Inset left & right by r)
        SDL_FRect topRect = new SDL_FRect { x = x + r, y = y, w = MathF.Max(0f, w - 2f * r), h = r };
        if (topRect.w > 0f) SDL3.SDL_RenderFillRect(_renderer, &topRect);

        // 3. Bottom-center rect (Inset left & right by r)
        SDL_FRect botRect = new SDL_FRect { x = x + r, y = y + h - r, w = MathF.Max(0f, w - 2f * r), h = r };
        if (botRect.w > 0f) SDL3.SDL_RenderFillRect(_renderer, &botRect);

        // 4. Render 4 quarter-circle corner fans
        int segments = 8;
        int totalVerts = 4 * (segments + 2);
        var verts = new SDL_Vertex[totalVerts];
        int idx = 0;

        float cr = color.R, cg = color.G, cb = color.B, ca = color.A;

        // Screen space (+Y down) quarter circle angles:
        var corners = new[] {
            (x + r,         y + r,         180f, 270f), // Top-Left
            (x + w - r,     y + r,         270f, 360f), // Top-Right
            (x + w - r,     y + h - r,     0f,   90f),  // Bottom-Right
            (x + r,         y + h - r,     90f,  180f), // Bottom-Left
        };

        foreach (var (cx, cy, startAngle, endAngle) in corners)
        {
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
