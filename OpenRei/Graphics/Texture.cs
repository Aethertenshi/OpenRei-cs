using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Wraps a hardware GPU texture handle (SDL_Texture*) with VRAM dimension tracking and reference counting.
/// </summary>
public unsafe class Texture : IDisposable
{
    private SDL_Texture* _handle;
    private bool _isDisposed;

    public SDL_Texture* Handle => _handle;
    public int Width { get; }
    public int Height { get; }
    public Vector2D Size => new Vector2D(Width, Height);
    public bool IsValid => _handle != null && !_isDisposed;

    private int _refCount = 1;

    /// <summary>
    /// Tracks active reference count across UI elements sharing this VRAM texture.
    /// </summary>
    public int RefCount => _refCount;

    public Texture(SDL_Texture* handle, int width, int height)
    {
        _handle = handle;
        Width = width;
        Height = height;
    }

    public void AddRef()
    {
        Interlocked.Increment(ref _refCount);
    }

    public void ReleaseRef()
    {
        Interlocked.Decrement(ref _refCount);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_handle != null)
            {
                SDL3.SDL_DestroyTexture(_handle);
                _handle = null;
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
