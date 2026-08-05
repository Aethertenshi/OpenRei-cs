namespace reistar.Renderer.SDL3;

using System;
using System.Collections.Generic;
using SDL;
using reistar.Core;
using reistar.Graphics;
using reistar.Maths;

public unsafe class SdlRenderer : IRenderer, IDisposable
{
    private readonly SdlWindow _window;
    private SDL_Renderer* _renderer;
    private RenderCommand[] _commandBuffer = new RenderCommand[1024];
    private int _commandCount = 0;
    private uint _submissionCounter = 0;

    public SdlWindow Window => _window;
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
            Color = color
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
            Color = color,
            Thickness = thickness
        };
    }

    public void DrawCircle(Vect2D center, float radius, Color color, int zIndex = 0) { }
    public void DrawLine(Vect2D start, Vect2D end, float thickness, Color color, int zIndex = 0) { }
    public void DrawTexture(ITexture texture, Vect2D position, Vect2D size, Color tint, int zIndex = 0) { }

    public void DrawTexturedQuad(ITexture texture, Vect2D position, Vect2D size, float u0, float v0, float u1, float v1, Color tint, int zIndex = 0)
    {
        EnsureCapacity();
        ulong key = ((ulong)(zIndex + (long)int.MaxValue) << 32) | _submissionCounter++;
        _commandBuffer[_commandCount++] = new RenderCommand
        {
            SortKey = key,
            Type = RenderPrimitiveType.TexturedQuad,
            Position = position,
            Size = size,
            Color = tint,
            U0 = u0,
            V0 = v0,
            U1 = u1,
            V1 = v1
        };
    }

    public void DrawText(Font font, string text, Vect2D position, float fontSize, Color color, int zIndex = 0)
    {
        if (font == null || string.IsNullOrEmpty(text)) return;

        float scale = fontSize / (font.PixelSize <= 0 ? 32f : font.PixelSize);
        float currentX = position.X;
        float currentY = position.Y;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (font.TryGetGlyph(c, out var glyph))
            {
                float x = currentX + (glyph.BearingX * scale);
                float y = currentY + ((font.PixelSize - glyph.BearingY) * scale);
                float w = glyph.Width * scale;
                float h = glyph.Height * scale;

                if (font.AtlasTexture != null && w > 0 && h > 0)
                {
                    DrawTexturedQuad(font.AtlasTexture, new Vect2D(x, y), new Vect2D(w, h), glyph.U0, glyph.V0, glyph.U1, glyph.V1, color, zIndex);
                }

                currentX += glyph.Advance * scale;
            }
            else if (c == ' ')
            {
                currentX += (fontSize * 0.33f);
            }
        }
    }

    public void EndFrame()
    {
        SDL3.SDL_SetRenderDrawColor(_renderer, 25, 25, 45, 255);
        SDL3.SDL_RenderClear(_renderer);

        if (_commandCount > 0)
        {
            Array.Sort(_commandBuffer, 0, _commandCount, CommandComparer.Instance);

            for (int i = 0; i < _commandCount; i++)
            {
                ref readonly var cmd = ref _commandBuffer[i];
                if (cmd.Type == RenderPrimitiveType.Rectangle)
                {
                    SDL3.SDL_SetRenderDrawColor(_renderer, cmd.Color.R, cmd.Color.G, cmd.Color.B, cmd.Color.A);
                    SDL_FRect rect = new SDL_FRect
                    {
                        x = cmd.Position.X,
                        y = cmd.Position.Y,
                        w = cmd.Size.X,
                        h = cmd.Size.Y
                    };
                    SDL3.SDL_RenderFillRect(_renderer, &rect);
                }
            }
        }

        SDL3.SDL_RenderPresent(_renderer);
    }

    private void EnsureCapacity()
    {
        if (_commandCount >= _commandBuffer.Length)
        {
            Array.Resize(ref _commandBuffer, _commandBuffer.Length * 2);
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
