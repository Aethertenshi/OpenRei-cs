namespace reistar.Renderer.SDL3;

using System;
using SDL;
using reistar.Graphics;

public unsafe class SdlTexture : ITexture
{
    private SDL_Texture* _handle;
    public int Width { get; }
    public int Height { get; }
    public ushort TextureId { get; }

    public SDL_Texture* Handle => _handle;

    public SdlTexture(SDL_Texture* handle, int width, int height, ushort id = 0)
    {
        _handle = handle;
        Width = width;
        Height = height;
        TextureId = id;
    }

    public void Dispose()
    {
        if (_handle != null)
        {
            SDL3.SDL_DestroyTexture(_handle);
            _handle = null;
        }
    }
}
