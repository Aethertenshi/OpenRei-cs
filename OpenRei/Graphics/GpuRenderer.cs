using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Per-frame SDL_GPU renderer. Acquires swapchain texture, issues draw calls for
/// color quads and textured text, and presents. Uses a string cache to avoid
/// re-rasterizing TTF glyphs every frame.
///
/// Threading: all methods must be called from the main render thread only.
/// No concurrent access is expected or handled.
/// </summary>
public unsafe class GpuRenderer : IDisposable
{
    // ── Vertex layouts (mirrors PoC) ───────────────────────────────────────────
    private struct ColorVertex { public float PX, PY; public float CR, CG, CB, CA; }
    private struct TexturedVertex { public float PX, PY; public float TX, TY; public float CR, CG, CB, CA; }

    // ── String cache ───────────────────────────────────────────────────────────
    private readonly struct TextCacheKey : IEquatable<TextCacheKey>
    {
        public readonly nint FontHandle;
        public readonly string Text;
        public readonly float R, G, B;
        public readonly float Size;

        public TextCacheKey(nint font, string text, Color color, float size)
        {
            FontHandle = font;
            Text = text;
            R = color.R; G = color.G; B = color.B;
            Size = size;
        }

        public bool Equals(TextCacheKey other) =>
            FontHandle == other.FontHandle && Text == other.Text &&
            R == other.R && G == other.G && B == other.B && Size == other.Size;

        public override bool Equals(object? obj) => obj is TextCacheKey k && Equals(k);
        public override int GetHashCode() =>
            HashCode.Combine(FontHandle, Text, R, G, B, Size);
    }

    private sealed class TextCacheEntry
    {
        public SDL_GPUTexture* Texture;
        public SDL_GPUBuffer* VertexBuffer;
        public int Width, Height;
        public long LastUsedFrame;
    }

    private readonly Dictionary<TextCacheKey, TextCacheEntry> _textCache = new();
    private const int MaxCacheEntries = 256;
    private long _frameIndex;

    // ── Device references ──────────────────────────────────────────────────────
    private readonly SDL_GPUDevice* _device;
    private readonly SDL_Window* _window;
    private readonly ShaderPipeline _pipelines;
    private readonly SDL_GPUTextureFormat _swapchainFormat;
    private int _drawW, _drawH;

    // ── Pre-allocated vertex buffers (color + text) ────────────────────────────
    private SDL_GPUBuffer* _colorVB;
    private readonly SDL_GPUSampler* _sampler;

    // Quad geometry (6 vertices, triangle list, covers NDC [-1,1] when needed)
    private static readonly float[] _fullScreenQuad = new float[]
    {
        // position (xy)     texcoord (uv)   color (rgba) — unused for color, set white
        -1f, -1f,  0f, 0f,  1f, 1f, 1f, 1f,
         1f, -1f,  1f, 0f,  1f, 1f, 1f, 1f,
        -1f,  1f,  0f, 1f,  1f, 1f, 1f, 1f,
         1f, -1f,  1f, 0f,  1f, 1f, 1f, 1f,
         1f,  1f,  1f, 1f,  1f, 1f, 1f, 1f,
        -1f,  1f,  0f, 1f,  1f, 1f, 1f, 1f,
    };

    public GpuRenderer(SDL_GPUDevice* device, SDL_Window* window, ShaderPipeline pipelines, SDL_GPUTextureFormat fmt)
    {
        _device = device;
        _window = window;
        _pipelines = pipelines;
        _swapchainFormat = fmt;

        // Create shared sampler for text rendering
        var sampInfo = new SDL_GPUSamplerCreateInfo
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE
        };
        _sampler = SDL3.SDL_CreateGPUSampler(_device, &sampInfo);

        // Upload a full-screen quad for background clears / test draws
        // For now use the color pipeline's position+color format — set up a simple VB
        UploadColorQuad();
    }

    private void UploadColorQuad()
    {
        // 6 vertices, ColorVertex = 24 bytes each
        uint vbSize = 6 * (uint)sizeof(ColorVertex);
        ColorVertex[] verts = new ColorVertex[6];
        // White, fully opaque
        for (int i = 0; i < 6; i++)
            verts[i] = new ColorVertex { PX = _fullScreenQuad[i * 8], PY = _fullScreenQuad[i * 8 + 1], CR = 1, CG = 1, CB = 1, CA = 1 };

        var stgInfo = new SDL_GPUTransferBufferCreateInfo { size = vbSize, usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD };
        var stg = SDL3.SDL_CreateGPUTransferBuffer(_device, &stgInfo);
        void* mapped = (void*)SDL3.SDL_MapGPUTransferBuffer(_device, stg, false);
        fixed (ColorVertex* src = verts)
            Buffer.MemoryCopy(src, mapped, vbSize, vbSize);
        SDL3.SDL_UnmapGPUTransferBuffer(_device, stg);

        var bi = new SDL_GPUBufferCreateInfo { size = vbSize, usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX };
        _colorVB = SDL3.SDL_CreateGPUBuffer(_device, &bi);
        var ucmd = SDL3.SDL_AcquireGPUCommandBuffer(_device);
        var cp = SDL3.SDL_BeginGPUCopyPass(ucmd);
        var sl = new SDL_GPUTransferBufferLocation { transfer_buffer = stg, offset = 0 };
        var dr = new SDL_GPUBufferRegion { buffer = _colorVB, offset = 0, size = vbSize };
        SDL3.SDL_UploadToGPUBuffer(cp, &sl, &dr, false);
        SDL3.SDL_EndGPUCopyPass(cp);
        SDL3.SDL_SubmitGPUCommandBuffer(ucmd);
        SDL3.SDL_ReleaseGPUTransferBuffer(_device, stg);
    }

    // ── Per-frame entry point ──────────────────────────────────────────────────

    public void Render(RenderContext context)
    {
        _frameIndex++;

        var cmdBuf = SDL3.SDL_AcquireGPUCommandBuffer(_device);
        if (cmdBuf == null) return;

        SDL_GPUTexture* swapTex = null;
        if (!SDL3.SDL_AcquireGPUSwapchainTexture(cmdBuf, _window, &swapTex, null, null))
        { SDL3.SDL_SubmitGPUCommandBuffer(cmdBuf); return; }
        if (swapTex == null)
        { SDL3.SDL_SubmitGPUCommandBuffer(cmdBuf); return; }

        int w, h;
        SDL3.SDL_GetWindowSizeInPixels(_window, &w, &h);
        _drawW = w; _drawH = h;

        var ct = new SDL_GPUColorTargetInfo
        {
            texture = swapTex,
            clear_color = new SDL_FColor { r = 0.07f, g = 0.07f, b = 0.12f, a = 1.0f },
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE
        };
        var pass = SDL3.SDL_BeginGPURenderPass(cmdBuf, &ct, 1, null);

        var vp = new SDL_GPUViewport { x = 0, y = 0, w = _drawW, h = _drawH, min_depth = 0, max_depth = 1 };
        SDL3.SDL_SetGPUViewport(pass, &vp);

        // ── 1. Render color quads from the context ─────────────────────────────
        // (future: batch all color quads into one vertex buffer)
        var colorHandle = _pipelines.ColorHandle;
        if (colorHandle != null)
        {
            SDL3.SDL_BindGPUGraphicsPipeline(pass, colorHandle);
            var vbBind = new SDL_GPUBufferBinding { buffer = _colorVB, offset = 0 };
            SDL3.SDL_BindGPUVertexBuffers(pass, 0, &vbBind, 1);
            SDL3.SDL_DrawGPUPrimitives(pass, 6, 1, 0, 0);
        }

        // ── 2. Render text commands ────────────────────────────────────────────
        var texturedHandle = _pipelines.TexturedHandle;
        if (texturedHandle != null && context.TextCommands.Count > 0)
        {
            SDL3.SDL_BindGPUGraphicsPipeline(pass, texturedHandle);

            foreach (var textCmd in context.TextCommands)
            {
                if (string.IsNullOrEmpty(textCmd.Text)) continue;

                var texEntry = GetOrCreateTextTexture(textCmd);
                if (texEntry == null) continue;

                // Each cached entry has its own vertex buffer sized to its texel dimensions
                if (texEntry.VertexBuffer != null)
                {
                    var tvBind = new SDL_GPUBufferBinding { buffer = texEntry.VertexBuffer, offset = 0 };
                    SDL3.SDL_BindGPUVertexBuffers(pass, 0, &tvBind, 1);
                }

                var texBind = new SDL_GPUTextureSamplerBinding { texture = texEntry.Texture, sampler = _sampler };
                SDL3.SDL_BindGPUFragmentSamplers(pass, 0, &texBind, 1);

                SDL3.SDL_DrawGPUPrimitives(pass, 6, 1, 0, 0);
            }
        }

        SDL3.SDL_EndGPURenderPass(pass);
        SDL3.SDL_SubmitGPUCommandBuffer(cmdBuf);
    }

    // ── Text cache ─────────────────────────────────────────────────────────────

    private TextCacheEntry? GetOrCreateTextTexture(TextCommand cmd)
    {
        var font = cmd.Font ?? FontEngine.DefaultFont;
        if (font?.Handle == null) return null;

        var key = new TextCacheKey((nint)font.Handle, cmd.Text, cmd.Color, font.Size);

        // Cache hit
        if (_textCache.TryGetValue(key, out var entry))
        {
            entry.LastUsedFrame = _frameIndex;
            return entry;
        }

        // Evict if at capacity
        if (_textCache.Count >= MaxCacheEntries)
        {
            long oldestFrame = _frameIndex;
            TextCacheKey oldestKey = default;
            foreach (var kv in _textCache)
            {
                if (kv.Value.LastUsedFrame < oldestFrame)
                {
                    oldestFrame = kv.Value.LastUsedFrame;
                    oldestKey = kv.Key;
                }
            }
            if (_textCache.Remove(oldestKey, out var removed))
            {
                SDL3.SDL_ReleaseGPUTexture(_device, removed.Texture);
                if (removed.VertexBuffer != null)
                    SDL3.SDL_ReleaseGPUBuffer(_device, removed.VertexBuffer);
            }
        }

        // Rasterize new text texture (hot-path: this runs once per unique string)
        string text = cmd.Text;
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(text + "\0");
        var sdlColor = new SDL_Color
        {
            r = (byte)(cmd.Color.R * 255),
            g = (byte)(cmd.Color.G * 255),
            b = (byte)(cmd.Color.B * 255),
            a = (byte)(cmd.Color.A * 255)
        };

        SDL_Surface* surface;
        fixed (byte* t = utf8)
            surface = SDL3_ttf.TTF_RenderText_Blended(font.Handle, t, (nuint)(utf8.Length - 1), sdlColor);
        if (surface == null) return null;

        // Convert to RGBA8888
        SDL_Surface* converted = SDL3.SDL_ConvertSurface(surface, SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888);
        SDL3.SDL_DestroySurface(surface);
        if (converted == null) return null;

        int texW = converted->w, texH = converted->h;

        // Create GPU texture
        var texInfo = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
            width = (uint)texW, height = (uint)texH,
            layer_count_or_depth = 1, num_levels = 1,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER
        };
        var gpuTex = SDL3.SDL_CreateGPUTexture(_device, &texInfo);
        if (gpuTex == null) { SDL3.SDL_DestroySurface(converted); return null; }

        // Upload via staging buffer
        uint bpp = 4;
        uint srcPitch = (uint)converted->pitch;
        uint tightPitch = (uint)texW * bpp;
        uint dataSize = tightPitch * (uint)texH;

        var stgInfo = new SDL_GPUTransferBufferCreateInfo { size = dataSize, usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD };
        var stg = SDL3.SDL_CreateGPUTransferBuffer(_device, &stgInfo);
        byte* mapped = (byte*)SDL3.SDL_MapGPUTransferBuffer(_device, stg, false);
        byte* srcRow = (byte*)converted->pixels;
        for (int y = 0; y < texH; y++)
        {
            Buffer.MemoryCopy(srcRow, mapped + (y * tightPitch), tightPitch, tightPitch);
            srcRow += srcPitch;
        }
        SDL3.SDL_UnmapGPUTransferBuffer(_device, stg);
        SDL3.SDL_DestroySurface(converted);

        var ucmd = SDL3.SDL_AcquireGPUCommandBuffer(_device);
        var cp = SDL3.SDL_BeginGPUCopyPass(ucmd);
        var srcLoc = new SDL_GPUTextureTransferInfo
        {
            transfer_buffer = stg, offset = 0,
            pixels_per_row = (uint)texW,
            rows_per_layer = (uint)texH
        };
        var dstRegion = new SDL_GPUTextureRegion { texture = gpuTex, w = (uint)texW, h = (uint)texH, d = 1 };
        SDL3.SDL_UploadToGPUTexture(cp, &srcLoc, &dstRegion, false);
        SDL3.SDL_EndGPUCopyPass(cp);
        SDL3.SDL_SubmitGPUCommandBuffer(ucmd);
        SDL3.SDL_ReleaseGPUTransferBuffer(_device, stg);

        // Create per-entry vertex buffer sized to the text texture's pixel dimensions
        var vb = CreateTextQuadBuffer(texW, texH);

        var cacheEntry = new TextCacheEntry
        {
            Texture = gpuTex, VertexBuffer = vb,
            Width = texW, Height = texH,
            LastUsedFrame = _frameIndex
        };
        _textCache[key] = cacheEntry;
        return cacheEntry;
    }

    /// <summary>Creates a GPU vertex buffer for a textured quad sized to the given texel dimensions.</summary>
    private SDL_GPUBuffer* CreateTextQuadBuffer(int texW, int texH)
    {
        float tw = (float)texW / _drawW;
        float th = (float)texH / _drawH;

        TexturedVertex[] verts = new TexturedVertex[6];
        verts[0] = new TexturedVertex { PX = -tw, PY = -th, TX = 0, TY = 1, CR = 1, CG = 1, CB = 1, CA = 1 };
        verts[1] = new TexturedVertex { PX =  tw, PY = -th, TX = 1, TY = 1, CR = 1, CG = 1, CB = 1, CA = 1 };
        verts[2] = new TexturedVertex { PX = -tw, PY =  th, TX = 0, TY = 0, CR = 1, CG = 1, CB = 1, CA = 1 };
        verts[3] = new TexturedVertex { PX =  tw, PY = -th, TX = 1, TY = 1, CR = 1, CG = 1, CB = 1, CA = 1 };
        verts[4] = new TexturedVertex { PX =  tw, PY =  th, TX = 1, TY = 0, CR = 1, CG = 1, CB = 1, CA = 1 };
        verts[5] = new TexturedVertex { PX = -tw, PY =  th, TX = 0, TY = 0, CR = 1, CG = 1, CB = 1, CA = 1 };

        uint vbSize = (uint)(sizeof(TexturedVertex) * verts.Length);
        var stgInfo = new SDL_GPUTransferBufferCreateInfo { size = vbSize, usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD };
        var stg = SDL3.SDL_CreateGPUTransferBuffer(_device, &stgInfo);
        void* mapped = (void*)SDL3.SDL_MapGPUTransferBuffer(_device, stg, false);
        fixed (TexturedVertex* src = verts)
            Buffer.MemoryCopy(src, mapped, vbSize, vbSize);
        SDL3.SDL_UnmapGPUTransferBuffer(_device, stg);

        var bi = new SDL_GPUBufferCreateInfo { size = vbSize, usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX };
        var vbBuffer = SDL3.SDL_CreateGPUBuffer(_device, &bi);
        var ucmd = SDL3.SDL_AcquireGPUCommandBuffer(_device);
        var cp = SDL3.SDL_BeginGPUCopyPass(ucmd);
        var sl = new SDL_GPUTransferBufferLocation { transfer_buffer = stg, offset = 0 };
        var dr = new SDL_GPUBufferRegion { buffer = vbBuffer, offset = 0, size = vbSize };
        SDL3.SDL_UploadToGPUBuffer(cp, &sl, &dr, false);
        SDL3.SDL_EndGPUCopyPass(cp);
        SDL3.SDL_SubmitGPUCommandBuffer(ucmd);
        SDL3.SDL_ReleaseGPUTransferBuffer(_device, stg);
        return vbBuffer;
    }

    public void Dispose()
    {
        // Release all cached text textures + vertex buffers
        foreach (var entry in _textCache.Values)
        {
            SDL3.SDL_ReleaseGPUTexture(_device, entry.Texture);
            if (entry.VertexBuffer != null)
                SDL3.SDL_ReleaseGPUBuffer(_device, entry.VertexBuffer);
        }
        _textCache.Clear();

        if (_sampler != null) SDL3.SDL_ReleaseGPUSampler(_device, _sampler);
        if (_colorVB != null) SDL3.SDL_ReleaseGPUBuffer(_device, _colorVB);
    }
}
