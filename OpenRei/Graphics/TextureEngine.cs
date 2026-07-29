using System.Collections.Concurrent;
using System.Diagnostics;
using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Thread-safe, time-budgeted asynchronous Texture Engine managing background disk I/O, CPU image decoding, and VRAM upload.
/// Robust error handling for cross-platform Linux/Windows image codecs (JPG, PNG, WEBP).
/// </summary>
public static unsafe class TextureEngine
{
    private static bool _isInitialized;
    private static readonly ConcurrentDictionary<string, Texture> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Task<Texture?>> _pendingLoads = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<PendingTextureUpload> _uploadQueue = new();

    private static Texture? _fallbackTexture;

    public static bool IsInitialized => _isInitialized;

    public static void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        Console.WriteLine("[OpenRei TextureEngine] SDL3_image initialized successfully.");
    }

    /// <summary>
    /// Asynchronously loads a texture off-thread without blocking the main render loop.
    /// Returns cached texture handle if already loaded.
    /// </summary>
    public static Task<Texture?> LoadAsync(string path)
    {
        Initialize();

        if (string.IsNullOrEmpty(path)) return Task.FromResult<Texture?>(null);

        // 1. Check if already in VRAM cache
        if (_cache.TryGetValue(path, out var cachedTexture) && cachedTexture.IsValid)
        {
            cachedTexture.AddRef();
            return Task.FromResult<Texture?>(cachedTexture);
        }

        // 2. Prevent duplicate concurrent background loads for the same asset
        return _pendingLoads.GetOrAdd(path, assetPath => Task.Run(() =>
        {
            string[] candidatePaths = new string[]
            {
                assetPath,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assetPath),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenRei", assetPath),
                Path.Combine("OpenRei", assetPath),
                Path.Combine("..", "OpenRei", assetPath),
                Path.Combine("..", "..", "OpenRei", assetPath)
            };

            string? resolvedPath = candidatePaths.FirstOrDefault(File.Exists);

            if (resolvedPath == null)
            {
                Console.WriteLine($"[TextureEngine Warning] Image file not found in search paths: '{assetPath}'");
                return null;
            }

            byte[] pathBytes = System.Text.Encoding.UTF8.GetBytes(resolvedPath + "\0");
            SDL_Surface* surface = null;

            fixed (byte* pathPtr = pathBytes)
            {
                surface = SDL3_image.IMG_Load(pathPtr);
            }

            if (surface == null || surface->w <= 0 || surface->h <= 0 || (nint)surface->pixels == 0)
            {
                Console.WriteLine($"[TextureEngine Warning] Failed to decode image '{resolvedPath}': {SDL3.SDL_GetError()}");
                if (surface != null) SDL3.SDL_DestroySurface(surface);
                return null;
            }

            // Enqueue decoded surface for main-thread VRAM upload
            var tcs = new TaskCompletionSource<Texture?>();
            _uploadQueue.Enqueue(new PendingTextureUpload(assetPath, surface, tcs));
            return tcs.Task;
        })).ContinueWith(t =>
        {
            _pendingLoads.TryRemove(path, out _);
            return t.Result;
        }, TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <summary>
    /// Processes pending texture uploads from the background queue within a strict main-thread time budget (ms).
    /// </summary>
    public static void ProcessPendingUploads(GraphicsDevice graphicsDevice, float maxTimeMs = 2.0f)
    {
        if (graphicsDevice == null || !graphicsDevice.IsInitialized) return;

        var stopwatch = Stopwatch.StartNew();

        while (_uploadQueue.TryDequeue(out var pending))
        {
            if (pending.Surface == null || pending.Surface->w <= 0 || pending.Surface->h <= 0 || (nint)pending.Surface->pixels == 0)
            {
                pending.Tcs?.TrySetResult(null);
                continue;
            }

            SDL_Texture* gpuTexture = SDL3.SDL_CreateTextureFromSurface(graphicsDevice.RendererHandle, pending.Surface);
            int width = pending.Surface->w;
            int height = pending.Surface->h;

            // Immediately destroy CPU surface RAM
            SDL3.SDL_DestroySurface(pending.Surface);

            Texture? textureResult = null;
            if (gpuTexture != null)
            {
                textureResult = new Texture(gpuTexture, width, height);
                _cache[pending.Key] = textureResult;
                Console.WriteLine($"[TextureEngine] VRAM Texture '{pending.Key}' ({width}x{height}) uploaded successfully.");
            }
            else
            {
                Console.WriteLine($"[TextureEngine Error] VRAM creation failed for '{pending.Key}': {SDL3.SDL_GetError()}");
            }

            pending.Tcs?.TrySetResult(textureResult);

            if (stopwatch.Elapsed.TotalMilliseconds >= maxTimeMs)
            {
                break; // Preserve main-thread frame budget
            }
        }
    }

    /// <summary>
    /// Retrieves a cached texture synchronously by path.
    /// </summary>
    public static Texture? Get(string path)
    {
        return _cache.TryGetValue(path, out var tex) && tex.IsValid ? tex : null;
    }

    /// <summary>
    /// Returns a 1x1 magenta fallback texture to prevent rendering crashes on missing assets.
    /// </summary>
    public static Texture GetFallbackTexture(GraphicsDevice graphicsDevice)
    {
        if (_fallbackTexture != null && _fallbackTexture.IsValid) return _fallbackTexture;

        if (graphicsDevice != null && graphicsDevice.IsInitialized)
        {
            SDL_Surface* surface = SDL3.SDL_CreateSurface(1, 1, SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888);
            if (surface != null)
            {
                uint* pixels = (uint*)surface->pixels;
                *pixels = 0xFF00FFFF; // Magenta (RGBA)
                SDL_Texture* gpuTex = SDL3.SDL_CreateTextureFromSurface(graphicsDevice.RendererHandle, surface);
                SDL3.SDL_DestroySurface(surface);

                if (gpuTex != null)
                {
                    _fallbackTexture = new Texture(gpuTex, 1, 1);
                    return _fallbackTexture;
                }
            }
        }

        return new Texture(null, 1, 1);
    }

    /// <summary>
    /// Releases an unused texture from VRAM cache when reference count drops to 0.
    /// </summary>
    public static void Release(string path)
    {
        if (_cache.TryGetValue(path, out var texture))
        {
            texture.ReleaseRef();
            if (texture.RefCount <= 0)
            {
                if (_cache.TryRemove(path, out var removedTexture))
                {
                    removedTexture.Dispose();
                    Console.WriteLine($"[TextureEngine] VRAM Texture '{path}' released and disposed.");
                }
            }
        }
    }

    public static void Shutdown()
    {
        if (_isInitialized)
        {
            foreach (var tex in _cache.Values)
            {
                tex.Dispose();
            }
            _cache.Clear();
            _fallbackTexture?.Dispose();
            _fallbackTexture = null;
            _isInitialized = false;
        }
    }
}
