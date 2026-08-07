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
            Thickness = thickness,
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
            Thickness = thickness,
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

        TTF_Font* handle = font.GetHandle(fontSize);
        if (handle == null) return;

        SDL_Color sdlColor = new SDL_Color
        {
            r = color.R,
            g = color.G,
            b = color.B,
            a = color.A
        };

        SDL_Surface* surface = SDL3_ttf.TTF_RenderText_Blended(handle, text, (nuint)text.Length, sdlColor);
        if (surface == null) return;

        SDL_Texture* tex = SDL3.SDL_CreateTextureFromSurface(_renderer, surface);
        int surfW = surface->w;
        int surfH = surface->h;
        SDL3.SDL_DestroySurface(surface);

        if (tex == null) return;

        Vect2D measured = font.MeasureString(text, fontSize);
        Vect2D size = new Vect2D(
            measured.X > 0 ? measured.X : surfW,
            measured.Y > 0 ? measured.Y : surfH
        );

        ITexture tempTex = new SdlTexture(tex, (int)size.X, (int)size.Y);
        DrawTexture(tempTex, position, size, Color.White, zIndex);
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

            for (int i = 0; i < _commandCount; i++)
            {
                ref readonly var cmd = ref _commandBuffer[i];
                SDL3.SDL_SetRenderDrawColor(_renderer, cmd.Color.R, cmd.Color.G, cmd.Color.B, cmd.Color.A);

                switch (cmd.Type)
                {
                    case RenderPrimitiveType.Rectangle:
                        SDL_FRect fillRect = new SDL_FRect
                        {
                            x = cmd.Position.X,
                            y = cmd.Position.Y,
                            w = cmd.Size.X,
                            h = cmd.Size.Y
                        };
                        SDL3.SDL_RenderFillRect(_renderer, &fillRect);
                        break;

                    case RenderPrimitiveType.RectangleOutline:
                        SDL_FRect outlineRect = new SDL_FRect
                        {
                            x = cmd.Position.X,
                            y = cmd.Position.Y,
                            w = cmd.Size.X,
                            h = cmd.Size.Y
                        };
                        SDL3.SDL_RenderRect(_renderer, &outlineRect);
                        break;

                    case RenderPrimitiveType.Line:
                        SDL3.SDL_RenderLine(_renderer, cmd.Position.X, cmd.Position.Y, cmd.Size.X, cmd.Size.Y);
                        break;

                    case RenderPrimitiveType.Circle:
                        DrawCircleOutlineInternal(cmd.Position, cmd.Size.X);
                        break;

                    case RenderPrimitiveType.TexturedQuad:
                        RenderTexturedQuadInternal(cmd);
                        break;
                }
            }
        }

        SDL3.SDL_RenderPresent(_renderer);
    }

    private void DrawCircleOutlineInternal(Vect2D center, float radius)
    {
        int segments = Math.Max(16, (int)(radius * 0.5f));
        float angleStep = (MathF.PI * 2f) / segments;

        for (int i = 0; i < segments; i++)
        {
            float a1 = i * angleStep;
            float a2 = (i + 1) * angleStep;

            float x1 = center.X + MathF.Cos(a1) * radius;
            float y1 = center.Y + MathF.Sin(a1) * radius;
            float x2 = center.X + MathF.Cos(a2) * radius;
            float y2 = center.Y + MathF.Sin(a2) * radius;

            SDL3.SDL_RenderLine(_renderer, x1, y1, x2, y2);
        }
    }

    private void RenderTexturedQuadInternal(in RenderCommand cmd)
    {
        SDL_Texture* sdlTex = (cmd.Texture is SdlTexture texWrapper) ? texWrapper.Handle : null;

        SDL_FColor color = new SDL_FColor
        {
            r = cmd.Color.R / 255f,
            g = cmd.Color.G / 255f,
            b = cmd.Color.B / 255f,
            a = cmd.Color.A / 255f
        };

        SDL_Vertex* verts = stackalloc SDL_Vertex[4];
        verts[0] = new SDL_Vertex { position = new SDL_FPoint { x = cmd.Position.X, y = cmd.Position.Y }, color = color, tex_coord = new SDL_FPoint { x = cmd.U0, y = cmd.V0 } };
        verts[1] = new SDL_Vertex { position = new SDL_FPoint { x = cmd.Position.X + cmd.Size.X, y = cmd.Position.Y }, color = color, tex_coord = new SDL_FPoint { x = cmd.U1, y = cmd.V0 } };
        verts[2] = new SDL_Vertex { position = new SDL_FPoint { x = cmd.Position.X + cmd.Size.X, y = cmd.Position.Y + cmd.Size.Y }, color = color, tex_coord = new SDL_FPoint { x = cmd.U1, y = cmd.V1 } };
        verts[3] = new SDL_Vertex { position = new SDL_FPoint { x = cmd.Position.X, y = cmd.Position.Y + cmd.Size.Y }, color = color, tex_coord = new SDL_FPoint { x = cmd.U0, y = cmd.V1 } };

        int* indices = stackalloc int[6] { 0, 1, 2, 2, 3, 0 };

        if (sdlTex == null)
        {
            // Fallback fill rect if texture is null
            SDL_FRect fillRect = new SDL_FRect
            {
                x = cmd.Position.X,
                y = cmd.Position.Y,
                w = cmd.Size.X,
                h = cmd.Size.Y
            };
            SDL3.SDL_RenderFillRect(_renderer, &fillRect);
        }
        else
        {
            SDL3.SDL_RenderGeometry(_renderer, sdlTex, verts, 4, indices, 6);
        }
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
