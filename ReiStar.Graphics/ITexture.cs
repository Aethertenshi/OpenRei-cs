namespace reistar.Graphics;

public interface ITexture : IDisposable
{
    int Width { get; }
    int Height { get; }
    ushort TextureId { get; }
    void UpdateTexture(byte[] rgbaPixels);
}
