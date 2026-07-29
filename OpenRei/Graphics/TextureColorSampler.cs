using System.Collections.Concurrent;
using System.IO;
using OpenRei.Types;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Samples and caches the average color of texture images using a deterministic grid pattern.
/// Results are cached in-memory and persisted to a .bin file for instant reuse on subsequent runs.
/// Thread-safe.
/// </summary>
public static class TextureColorSampler
{
    private static readonly ConcurrentDictionary<string, Color> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static string? _cachePath;
    private static readonly object _fileLock = new();

    private const uint Magic = 0x494F5244; // "CIRD" LE magic for byte-correct RGBA32 cache

    /// <summary>Call once at startup with a writable base directory.</summary>
    public static void Initialize(string baseDirectory)
    {
        _cachePath = Path.Combine(baseDirectory, "cache", "texture_colors.bin");
        LoadCache();
    }

    /// <summary>
    /// Returns the average color of the texture at <paramref name="texturePath"/>.
    /// Samples a deterministic grid of <paramref name="samples"/> points (default 256 points).
    /// Results are cached in-memory and persisted to disk.
    /// Returns Color.White if the texture cannot be loaded.
    /// </summary>
    public static Color GetAverage(string texturePath, int samples = 256)
    {
        if (string.IsNullOrEmpty(texturePath))
            return Color.White;

        // Fast path — already cached
        if (_cache.TryGetValue(texturePath, out var cached))
            return cached;

        // Sample
        Color avg = SampleTexture(texturePath, samples);

        // Cache in memory + persist
        _cache[texturePath] = avg;
        AppendToCacheFile(texturePath, avg);
        return avg;
    }

    private static Color SampleTexture(string path, int samples)
    {
        // Resolve file path
        string? resolved = ResolvePath(path);
        if (resolved == null)
            return Color.White;

        unsafe
        {
            // Load surface
            byte[] pathBytes = System.Text.Encoding.UTF8.GetBytes(resolved + "\0");
            SDL_Surface* surface;
            fixed (byte* p = pathBytes)
                surface = SDL3_image.IMG_Load(p);

            if (surface == null)
                return Color.White;

            int w = surface->w;
            int h = surface->h;
            if (w <= 0 || h <= 0)
            {
                SDL3.SDL_DestroySurface(surface);
                return Color.White;
            }

            // Determine grid dimensions (default 16x16 = 256 grid points for true visual coverage)
            int gridSize = (int)MathF.Ceiling(MathF.Sqrt(samples));
            int totalPixels = gridSize * gridSize;

            // Convert surface to ABGR8888 (guarantees byte 0=R, 1=G, 2=B, 3=A in memory order on Little-Endian x86_64)
            SDL_Surface* converted = SDL3.SDL_ConvertSurface(surface, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888);
            SDL3.SDL_DestroySurface(surface);
            if (converted == null)
                return Color.White;

            int bpp = 4;
            int pitch = converted->pitch;
            byte* pixels = (byte*)converted->pixels;

            long sumR = 0, sumG = 0, sumB = 0;
            int count = 0;

            for (int gy = 0; gy < gridSize && count < totalPixels; gy++)
            {
                for (int gx = 0; gx < gridSize && count < totalPixels; gx++)
                {
                    int px = (int)((gx + 0.5f) * w / gridSize);
                    int py = (int)((gy + 0.5f) * h / gridSize);
                    px = Math.Clamp(px, 0, w - 1);
                    py = Math.Clamp(py, 0, h - 1);

                    int offset = py * pitch + px * bpp;
                    byte r = pixels[offset + 0];
                    byte g = pixels[offset + 1];
                    byte b = pixels[offset + 2];
                    byte a = pixels[offset + 3];

                    // Skip fully transparent pixels
                    if (a < 30) continue;

                    sumR += r;
                    sumG += g;
                    sumB += b;
                    count++;
                }
            }

            SDL3.SDL_DestroySurface(converted);

            if (count == 0)
                return Color.White;

            float inv = 1f / count;
            return new Color(sumR * inv / 255f, sumG * inv / 255f, sumB * inv / 255f, 1f);
        }
    }

    private static string? ResolvePath(string path)
    {
        if (File.Exists(path)) return path;
        string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        if (File.Exists(basePath)) return basePath;
        return null;
    }

    // ── Persistence ──────────────────────────────────────────────────────

    private static void LoadCache()
    {
        if (_cachePath == null || !File.Exists(_cachePath))
            return;

        try
        {
            byte[] data;
            lock (_fileLock)
            {
                data = File.ReadAllBytes(_cachePath);
            }

            if (data.Length < 8)
                return;

            uint magic = BitConverter.ToUInt32(data, 0);
            if (magic != Magic)
            {
                // Old/incompatible cache format — delete corrupted file to re-sample
                File.Delete(_cachePath);
                return;
            }

            int count = BitConverter.ToInt32(data, 4);
            int offset = 8;

            for (int i = 0; i < count && offset + 3 < data.Length; i++)
            {
                if (offset + 2 > data.Length) break;
                int pathLen = BitConverter.ToUInt16(data, offset);
                offset += 2;

                if (offset + pathLen + 3 > data.Length) break;
                string path = System.Text.Encoding.UTF8.GetString(data, offset, pathLen);
                offset += pathLen;

                byte r = data[offset++];
                byte g = data[offset++];
                byte b = data[offset++];

                _cache[path] = Color.FromRgba(r, g, b);
            }
        }
        catch
        {
            // Corrupted cache file — ignore, will rebuild
        }
    }

    private static void AppendToCacheFile(string path, Color color)
    {
        if (_cachePath == null) return;

        // Ensure directory exists
        string? dir = Path.GetDirectoryName(_cachePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        byte[] pathBytes = System.Text.Encoding.UTF8.GetBytes(path);
        int entrySize = 2 + pathBytes.Length + 3;
        byte[] entry = new byte[entrySize];

        // Path length (ushort)
        entry[0] = (byte)(pathBytes.Length & 0xFF);
        entry[1] = (byte)((pathBytes.Length >> 8) & 0xFF);

        // Path
        Array.Copy(pathBytes, 0, entry, 2, pathBytes.Length);

        // RGB
        int off = 2 + pathBytes.Length;
        entry[off + 0] = (byte)(color.R * 255f);
        entry[off + 1] = (byte)(color.G * 255f);
        entry[off + 2] = (byte)(color.B * 255f);

        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(_cachePath))
                {
                    // Write header + first entry
                    using var stream = new FileStream(_cachePath, FileMode.Create, FileAccess.Write);
                    byte[] header = new byte[8];
                    BitConverter.TryWriteBytes(header.AsSpan(0, 4), Magic);
                    BitConverter.TryWriteBytes(header.AsSpan(4, 4), 1);
                    stream.Write(header, 0, 8);
                    stream.Write(entry, 0, entrySize);
                }
                else
                {
                    // Append entry and update count
                    using var stream = new FileStream(_cachePath, FileMode.Open, FileAccess.ReadWrite);
                    stream.Seek(4, SeekOrigin.Begin);
                    byte[] countBytes = new byte[4];
                    stream.ReadExactly(countBytes, 0, 4);
                    int count = BitConverter.ToInt32(countBytes, 0);

                    count++;
                    stream.Seek(4, SeekOrigin.Begin);
                    BitConverter.TryWriteBytes(countBytes.AsSpan(0, 4), count);
                    stream.Write(countBytes, 0, 4);

                    stream.Seek(0, SeekOrigin.End);
                    stream.Write(entry, 0, entrySize);
                }
            }
            catch
            {
                // Best-effort cache persistence
            }
        }
    }
}
