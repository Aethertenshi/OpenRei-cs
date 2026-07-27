using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Container for an unmanaged C surface pointer decoded on background worker threads awaiting main-thread VRAM upload.
/// </summary>
public unsafe readonly struct PendingTextureUpload
{
    public readonly string Key;
    public readonly SDL_Surface* Surface;
    public readonly TaskCompletionSource<Texture?>? Tcs;

    public PendingTextureUpload(string key, SDL_Surface* surface, TaskCompletionSource<Texture?>? tcs = null)
    {
        Key = key;
        Surface = surface;
        Tcs = tcs;
    }
}
