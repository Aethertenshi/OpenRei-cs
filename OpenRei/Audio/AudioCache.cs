using System.Collections.Concurrent;

namespace OpenRei.Audio;

public static class AudioCache
{
    private static readonly ConcurrentDictionary<string, DecodedAudioData> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Task<DecodedAudioData?>> _pending = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<WeakReference<AudioStream>> _trackedStreams = new();
    private static readonly List<WeakReference<SoundEffect>> _trackedEffects = new();

    private static long _cacheSizeBytes;
    private static long _maxCacheSizeBytes = 200 * 1024 * 1024; // 200 MB default

    public static long MaxCacheSizeBytes
    {
        get => Interlocked.Read(ref _maxCacheSizeBytes);
        set => Interlocked.Exchange(ref _maxCacheSizeBytes, value);
    }

    public static long CacheSizeBytes => Interlocked.Read(ref _cacheSizeBytes);

    /// <summary>
    /// Returns cached decoded data, or starts decoding on a background thread.
    /// </summary>
    public static Task<DecodedAudioData?> GetOrDecodeAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
            return Task.FromResult<DecodedAudioData?>(null);

        // 1. Check cache
        if (_cache.TryGetValue(path, out var cached))
            return Task.FromResult<DecodedAudioData?>(cached);

        // 2. Prevent duplicate background loads
        return _pending.GetOrAdd(path, assetPath => Task.Run(() =>
        {
            string[] searchPaths = new string[]
            {
                assetPath,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assetPath),
                Path.Combine("OpenRei", assetPath),
                Path.Combine("..", "OpenRei", assetPath),
            };

            string? resolvedPath = searchPaths.FirstOrDefault(File.Exists);
            if (resolvedPath == null)
            {
                Console.WriteLine($"[AudioCache] File not found: '{assetPath}'");
                return null;
            }

            var decoded = AudioDecoder.DecodeFile(resolvedPath);
            if (decoded == null || decoded.PcmData.Length == 0)
                return null;

            // Cache the decoded data (with memory budget check)
            long dataSize = decoded.PcmData.Length;
            EvictIfNeeded(dataSize);

            _cache[assetPath] = decoded;
            Interlocked.Add(ref _cacheSizeBytes, dataSize);
            Console.WriteLine($"[AudioCache] Cached '{assetPath}' ({dataSize / 1024:N0} KB)");

            return decoded;
        }));
    }

    /// <summary>
    /// Releases a previously cached entry (decrements ref or removes).
    /// </summary>
    public static void Release(string path)
    {
        if (_cache.TryRemove(path, out var removed))
        {
            Interlocked.Add(ref _cacheSizeBytes, -removed.PcmData.Length);
            Console.WriteLine($"[AudioCache] Released '{path}'");
        }
    }

    internal static void Track(AudioStream stream)
    {
        lock (_trackedStreams)
        {
            _trackedStreams.Add(new WeakReference<AudioStream>(stream));
        }
    }

    internal static void Track(SoundEffect sfx)
    {
        lock (_trackedEffects)
        {
            _trackedEffects.Add(new WeakReference<SoundEffect>(sfx));
        }
    }

    /// <summary>
    /// Called each frame from the main loop. Resolves pending async loads that
    /// were requested with Play() before the decode completed.
    /// </summary>
    public static void CheckPending()
    {
        lock (_trackedStreams)
        {
            for (int i = _trackedStreams.Count - 1; i >= 0; i--)
            {
                if (_trackedStreams[i].TryGetTarget(out var stream))
                    stream.CheckPendingLoad();
                else
                    _trackedStreams.RemoveAt(i);
            }
        }

        lock (_trackedEffects)
        {
            for (int i = _trackedEffects.Count - 1; i >= 0; i--)
            {
                if (_trackedEffects[i].TryGetTarget(out var sfx))
                    sfx.CheckPendingLoad();
                else
                    _trackedEffects.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Clears all cached audio data.
    /// </summary>
    public static void Clear()
    {
        _cache.Clear();
        _pending.Clear();
        Interlocked.Exchange(ref _cacheSizeBytes, 0);
    }

    private static void EvictIfNeeded(long neededBytes)
    {
        while (Interlocked.Read(ref _cacheSizeBytes) + neededBytes > Interlocked.Read(ref _maxCacheSizeBytes) && _cache.Count > 0)
        {
            var key = _cache.Keys.FirstOrDefault();
            if (key == null) break;
            if (_cache.TryRemove(key, out var evicted))
            {
                Interlocked.Add(ref _cacheSizeBytes, -evicted.PcmData.Length);
                Console.WriteLine($"[AudioCache] Evicted '{key}' ({evicted.PcmData.Length / 1024:N0} KB)");
            }
        }
    }
}
