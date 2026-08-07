namespace reistar.Renderer.SDL3;

using System;
using System.Collections.Generic;
using SDL;
using reistar.Core;
using reistar.Graphics;
using reistar.Maths;

public unsafe class SdlRenderer : IRenderer, IWindowProvider, IDisposable
{
    private readonly SdlWindow _window;
    private SDL_Renderer* _renderer;
    private RenderCommand[] _commandBuffer = new RenderCommand[1024];
    private int _commandCount = 0;
    private uint _submissionCounter = 0;

    // Geometry Batching Buffers
    private SDL_Vertex[] _batchVertices = new SDL_Vertex[4096];
    private int[] _batchIndices = new int[6144];
    private int _batchVertexCount = 0;
    private int _batchIndexCount = 0;
    private SDL_Texture* _currentBatchTexture = null;

    public IWindow Window => _window;
    public SdlWindow SdlWindowHandle => _window;
    public Vect2D CanvasSize => _window.Size;

    public SdlRenderer(string title = "ReiStar Game", int width = 1280, int height = 720)
        : this(new SdlWindow(title, width, height))
    {
    }

    public SdlRenderer(SdlWindow window)
    {
        _window = window;
        _renderer = SDL3.SDL_CreateRenderer(window.Handle, (byte*)null);
        if (_renderer == null)
        {
            throw new InvalidOperationException($"Failed to create SDL3 Renderer: {SDL3.SDL_GetError()}");
        }
    }

    public void BeginFrame()
    {
        _commandCount = 0;
        _submissionCounter = 0;
    }

    public void DrawRect(Vect2D position, Vect2D size, Color color, int zIndex = 0)
    {
        EnsureCapacity();
        ulong key = ((ulong)(zIndex + (long)int.MaxValue) << 32) | _submissionCounter++;
        _commandBuffer[_commandCount++] = new RenderCommand
        {
            SortKey = key,
            Type = RenderPrimitiveType.Rectangle,
            Position = position,
            Size = size,
            Color = color,
            U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f
        };
    }

    public void DrawRectOutline(Vect2D position, Vect2D size, float thickness, Color color, int zIndex = 0)
    {
        EnsureCapacity();
        ulong key = ((ulong)(zIndex + (long)int.MaxValue) << 32) | _submissionCounter++;
        _commandBuffer[_commandCount++] = new RenderCommand
        {
            SortKey = key,
            Type = RenderPrimitiveType.RectangleOutline,
            Position = position,
            Size = size,
            Thickness = thickness > 0 ? thickness : 1f,
            Color = color
        };
    }

    public void DrawCircle(Vect2D center, float radius, Color color, int zIndex = 0)
    {
        EnsureCapacity();
        ulong key = ((ulong)(zIndex + (long)int.MaxValue) << 32) | _submissionCounter++;
        _commandBuffer[_commandCount++] = new RenderCommand
        {
            SortKey = key,
            Type = RenderPrimitiveType.Circle,
            Position = center,
            Size = new Vect2D(radius, radius),
            Color = color
        };
    }

    public void DrawLine(Vect2D start, Vect2D end, float thickness, Color color, int zIndex = 0)
    {
        EnsureCapacity();
        ulong key = ((ulong)(zIndex + (long)int.MaxValue) << 32) | _submissionCounter++;
        _commandBuffer[_commandCount++] = new RenderCommand
        {
            SortKey = key,
            Type = RenderPrimitiveType.Line,
            Position = start,
            Size = end,
            Thickness = thickness > 0 ? thickness : 1f,
            Color = color
        };
    }

    public void DrawTexture(ITexture texture, Vect2D position, Vect2D size, Color tint, int zIndex = 0)
    {
        DrawTexturedQuad(texture, position, size, 0f, 0f, 1f, 1f, tint, zIndex);
    }

    public void DrawTexturedQuad(ITexture? texture, Vect2D position, Vect2D size, float u0, float v0, float u1, float v1, Color tint, int zIndex = 0)
    {
        EnsureCapacity();
        ulong key = ((ulong)(zIndex + (long)int.MaxValue) << 32) | _submissionCounter++;
        _commandBuffer[_commandCount++] = new RenderCommand
        {
            SortKey = key,
            Type = RenderPrimitiveType.TexturedQuad,
            Position = position,
            Size = size,
            Texture = texture,
            U0 = u0,
            V0 = v0,
            U1 = u1,
            V1 = v1,
            Color = tint
        };
    }

    public void DrawText(Font font, string text, Vect2D position, float fontSize, Color color, int zIndex = 0)
    {
        if (font == null || string.IsNullOrEmpty(text) || color.A == 0) return;

        float currentX = position.X;
        float currentY = position.Y;
        float baselineY = currentY + (fontSize * 0.75f);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (font.TryGetGlyph(c, fontSize, this, out var glyph))
            {
                if (glyph.Width > 0 && glyph.Height > 0 && font.AtlasTexture != null)
                {
                    float x = currentX + glyph.BearingX;
                    float y = baselineY - glyph.BearingY;
                    float w = glyph.Width;
                    float h = glyph.Height;

                    DrawTexturedQuad(font.AtlasTexture, new Vect2D(x, y), new Vect2D(w, h), glyph.U0, glyph.V0, glyph.U1, glyph.V1, color, zIndex);
                }

                currentX += glyph.Advance;
            }
            else if (c == ' ')
            {
                currentX += (fontSize * 0.33f);
            }
        }
    }


    public ITexture? CreateTexture(int width, int height, byte[] rgbaPixels)
    {
        if (_renderer == null || width <= 0 || height <= 0) return null;

        SDL_Texture* tex = SDL3.SDL_CreateTexture(_renderer, SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888, SDL_TextureAccess.SDL_TEXTUREACCESS_STATIC, width, height);
        if (tex == null) return null;

        if (rgbaPixels != null && rgbaPixels.Length >= width * height * 4)
        {
            fixed (byte* pixelPtr = rgbaPixels)
            {
                SDL3.SDL_UpdateTexture(tex, null, (nint)pixelPtr, width * 4);
            }
        }

        SDL3.SDL_SetTextureBlendMode(tex, SDL_BlendMode.SDL_BLENDMODE_BLEND);
        return new SdlTexture(tex, width, height);
    }

    public void EndFrame()
    {
        SDL3.SDL_SetRenderDrawColor(_renderer, 25, 25, 45, 255);
        SDL3.SDL_RenderClear(_renderer);

        if (_commandCount > 0)
        {
            Array.Sort(_commandBuffer, 0, _commandCount, CommandComparer.Instance);

            _batchVertexCount = 0;
            _batchIndexCount = 0;
            _currentBatchTexture = null;

            for (int i = 0; i < _commandCount; i++)
            {
                ref readonly var cmd = ref _commandBuffer[i];
                SDL_Texture* cmdTexHandle = (cmd.Texture is SdlTexture texWrapper) ? texWrapper.Handle : null;

                PrepareBatchForCommand(cmd, cmdTexHandle);

                switch (cmd.Type)
                {
                    case RenderPrimitiveType.Rectangle:
                        AppendRectangleQuad(cmd.Position, cmd.Size, cmd.Color, 0f, 0f, 1f, 1f);
                        break;

                    case RenderPrimitiveType.RectangleOutline:
                        AppendRectangleOutlineQuads(cmd.Position, cmd.Size, cmd.Thickness, cmd.Color);
                        break;

                    case RenderPrimitiveType.Line:
                        AppendLineQuad(cmd.Position, cmd.Size, cmd.Thickness, cmd.Color);
                        break;

                    case RenderPrimitiveType.Circle:
                        AppendCircleFan(cmd.Position, cmd.Size.X, cmd.Color);
                        break;

                    case RenderPrimitiveType.TexturedQuad:
                    case RenderPrimitiveType.Texture:
                        AppendRectangleQuad(cmd.Position, cmd.Size, cmd.Color, cmd.U0, cmd.V0, cmd.U1, cmd.V1);
                        break;

                    case RenderPrimitiveType.CustomPass:
                        if (cmd.RequiresPostProcessing)
                        {
                            ExecutePostProcessingPass(cmd);
                        }
                        break;
                }
            }

            FlushBatch();
        }

        SDL3.SDL_RenderPresent(_renderer);
    }

    private void PrepareBatchForCommand(in RenderCommand cmd, SDL_Texture* cmdTexHandle)
    {
        bool textureChanged = (cmdTexHandle != _currentBatchTexture);
        bool overflow = (_batchVertexCount + 64 >= _batchVertices.Length) || (_batchIndexCount + 96 >= _batchIndices.Length);

        if (cmd.RequiresPostProcessing || textureChanged || overflow)
        {
            FlushBatch();
            _currentBatchTexture = cmdTexHandle;
        }
    }

    private void FlushBatch()
    {
        if (_batchVertexCount == 0 || _batchIndexCount == 0) return;

        fixed (SDL_Vertex* vPtr = _batchVertices)
        fixed (int* iPtr = _batchIndices)
        {
            SDL3.SDL_RenderGeometry(_renderer, _currentBatchTexture, vPtr, _batchVertexCount, iPtr, _batchIndexCount);
        }

        _batchVertexCount = 0;
        _batchIndexCount = 0;
    }

    private void ExecutePostProcessingPass(in RenderCommand cmd)
    {
        // Detached Post-Processing Barrier Pass (e.g., Backdrop Blur, Custom Shaders, Screen Capture)
    }

    private void AppendRectangleQuad(Vect2D pos, Vect2D size, Color color, float u0, float v0, float u1, float v1)
    {
        EnsureBatchCapacity(4, 6);

        SDL_FColor sdlColor = new SDL_FColor
        {
            r = color.R / 255f,
            g = color.G / 255f,
            b = color.B / 255f,
            a = color.A / 255f
        };

        int baseIdx = _batchVertexCount;

        _batchVertices[_batchVertexCount++] = new SDL_Vertex { position = new SDL_FPoint { x = pos.X, y = pos.Y }, color = sdlColor, tex_coord = new SDL_FPoint { x = u0, y = v0 } };
        _batchVertices[_batchVertexCount++] = new SDL_Vertex { position = new SDL_FPoint { x = pos.X + size.X, y = pos.Y }, color = sdlColor, tex_coord = new SDL_FPoint { x = u1, y = v0 } };
        _batchVertices[_batchVertexCount++] = new SDL_Vertex { position = new SDL_FPoint { x = pos.X + size.X, y = pos.Y + size.Y }, color = sdlColor, tex_coord = new SDL_FPoint { x = u1, y = v1 } };
        _batchVertices[_batchVertexCount++] = new SDL_Vertex { position = new SDL_FPoint { x = pos.X, y = pos.Y + size.Y }, color = sdlColor, tex_coord = new SDL_FPoint { x = u0, y = v1 } };

        _batchIndices[_batchIndexCount++] = baseIdx + 0;
        _batchIndices[_batchIndexCount++] = baseIdx + 1;
        _batchIndices[_batchIndexCount++] = baseIdx + 2;
        _batchIndices[_batchIndexCount++] = baseIdx + 2;
        _batchIndices[_batchIndexCount++] = baseIdx + 3;
        _batchIndices[_batchIndexCount++] = baseIdx + 0;
    }

    private void AppendRectangleOutlineQuads(Vect2D pos, Vect2D size, float thickness, Color color)
    {
        float t = MathF.Max(1f, thickness);

        // Top line
        AppendRectangleQuad(new Vect2D(pos.X, pos.Y), new Vect2D(size.X, t), color, 0f, 0f, 1f, 1f);
        // Bottom line
        AppendRectangleQuad(new Vect2D(pos.X, pos.Y + size.Y - t), new Vect2D(size.X, t), color, 0f, 0f, 1f, 1f);
        // Left line
        AppendRectangleQuad(new Vect2D(pos.X, pos.Y + t), new Vect2D(t, MathF.Max(0f, size.Y - (t * 2f))), color, 0f, 0f, 1f, 1f);
        // Right line
        AppendRectangleQuad(new Vect2D(pos.X + size.X - t, pos.Y + t), new Vect2D(t, MathF.Max(0f, size.Y - (t * 2f))), color, 0f, 0f, 1f, 1f);
    }

    private void AppendLineQuad(Vect2D start, Vect2D end, float thickness, Color color)
    {
        EnsureBatchCapacity(4, 6);

        Vect2D dir = new Vect2D(end.X - start.X, end.Y - start.Y);
        float len = MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
        if (len <= 0.0001f) return;

        Vect2D perp = new Vect2D(-dir.Y / len, dir.X / len) * (thickness * 0.5f);

        SDL_FColor sdlColor = new SDL_FColor
        {
            r = color.R / 255f,
            g = color.G / 255f,
            b = color.B / 255f,
            a = color.A / 255f
        };

        int baseIdx = _batchVertexCount;

        _batchVertices[_batchVertexCount++] = new SDL_Vertex { position = new SDL_FPoint { x = start.X + perp.X, y = start.Y + perp.Y }, color = sdlColor, tex_coord = new SDL_FPoint { x = 0f, y = 0f } };
        _batchVertices[_batchVertexCount++] = new SDL_Vertex { position = new SDL_FPoint { x = end.X + perp.X, y = end.Y + perp.Y }, color = sdlColor, tex_coord = new SDL_FPoint { x = 1f, y = 0f } };
        _batchVertices[_batchVertexCount++] = new SDL_Vertex { position = new SDL_FPoint { x = end.X - perp.X, y = end.Y - perp.Y }, color = sdlColor, tex_coord = new SDL_FPoint { x = 1f, y = 1f } };
        _batchVertices[_batchVertexCount++] = new SDL_Vertex { position = new SDL_FPoint { x = start.X - perp.X, y = start.Y - perp.Y }, color = sdlColor, tex_coord = new SDL_FPoint { x = 0f, y = 1f } };

        _batchIndices[_batchIndexCount++] = baseIdx + 0;
        _batchIndices[_batchIndexCount++] = baseIdx + 1;
        _batchIndices[_batchIndexCount++] = baseIdx + 2;
        _batchIndices[_batchIndexCount++] = baseIdx + 2;
        _batchIndices[_batchIndexCount++] = baseIdx + 3;
        _batchIndices[_batchIndexCount++] = baseIdx + 0;
    }

    private void AppendCircleFan(Vect2D center, float radius, Color color)
    {
        int segments = Math.Max(16, (int)(radius * 0.5f));
        EnsureBatchCapacity(segments + 1, segments * 3);

        SDL_FColor sdlColor = new SDL_FColor
        {
            r = color.R / 255f,
            g = color.G / 255f,
            b = color.B / 255f,
            a = color.A / 255f
        };

        int centerIdx = _batchVertexCount;
        _batchVertices[_batchVertexCount++] = new SDL_Vertex { position = new SDL_FPoint { x = center.X, y = center.Y }, color = sdlColor, tex_coord = new SDL_FPoint { x = 0.5f, y = 0.5f } };

        float angleStep = (MathF.PI * 2f) / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            float x = center.X + MathF.Cos(angle) * radius;
            float y = center.Y + MathF.Sin(angle) * radius;
            _batchVertices[_batchVertexCount++] = new SDL_Vertex { position = new SDL_FPoint { x = x, y = y }, color = sdlColor, tex_coord = new SDL_FPoint { x = 0.5f, y = 0.5f } };
        }

        for (int i = 0; i < segments; i++)
        {
            int p1 = centerIdx + 1 + i;
            int p2 = centerIdx + 1 + ((i + 1) % segments);
            _batchIndices[_batchIndexCount++] = centerIdx;
            _batchIndices[_batchIndexCount++] = p1;
            _batchIndices[_batchIndexCount++] = p2;
        }
    }

    private void EnsureCapacity()
    {
        if (_commandCount >= _commandBuffer.Length)
        {
            Array.Resize(ref _commandBuffer, _commandBuffer.Length * 2);
        }
    }

    private void EnsureBatchCapacity(int addedVerts, int addedIndices)
    {
        if (_batchVertexCount + addedVerts >= _batchVertices.Length)
        {
            Array.Resize(ref _batchVertices, Math.Max(_batchVertexCount + addedVerts, _batchVertices.Length * 2));
        }
        if (_batchIndexCount + addedIndices >= _batchIndices.Length)
        {
            Array.Resize(ref _batchIndices, Math.Max(_batchIndexCount + addedIndices, _batchIndices.Length * 2));
        }
    }

    public void Dispose()
    {
        if (_renderer != null)
        {
            SDL3.SDL_DestroyRenderer(_renderer);
            _renderer = null;
        }
        _window.Dispose();
    }

    private sealed class CommandComparer : IComparer<RenderCommand>
    {
        public static readonly CommandComparer Instance = new();
        public int Compare(RenderCommand x, RenderCommand y) => x.SortKey.CompareTo(y.SortKey);
    }
}
