using System.Collections.Concurrent;

namespace OpenRei.Audio;

public static class AudioCache
{
    // Cache with proper LRU ordering — most recently used at tail of _lruList
    private static readonly Dictionary<string, (LinkedListNode<string> Node, DecodedAudioData Data)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> _lruList = new();
    private static readonly object _cacheLock = new();

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
    public static int CachedCount
    {
        get { lock (_cacheLock) return _cache.Count; }
    }

    /// <summary>
    /// Returns cached decoded data, or starts decoding on a background thread.
    /// LRU ordering is updated on each cache hit.
    /// </summary>
    public static Task<DecodedAudioData?> GetOrDecodeAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
            return Task.FromResult<DecodedAudioData?>(null);

        // 1. Fast path — check cache (under lock)
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(path, out var entry))
            {
                // Move to end of LRU list (most recently used)
                _lruList.Remove(entry.Node);
                _lruList.AddLast(entry.Node);
                return Task.FromResult<DecodedAudioData?>(entry.Data);
            }
        }

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

            long dataSize = decoded.PcmData.Length;
            EvictIfNeeded(dataSize);

            lock (_cacheLock)
            {
                var node = _lruList.AddLast(assetPath);
                _cache[assetPath] = (node, decoded);
            }

            Interlocked.Add(ref _cacheSizeBytes, dataSize);
            Console.WriteLine($"[AudioCache] Cached '{assetPath}' ({dataSize / 1024:N0} KB)");
            return decoded;
        }));
    }

    /// <summary>
    /// Promotes an entry to most-recently-used without returning its data.
    /// Useful for preloading: call GetOrDecodeAsync for preload, then Touch
    /// when the user actually navigates to that song.
    /// </summary>
    public static void Touch(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(path, out var entry))
            {
                _lruList.Remove(entry.Node);
                _lruList.AddLast(entry.Node);
            }
        }
    }

    /// <summary>
    /// Releases a previously cached entry and removes it from LRU tracking.
    /// </summary>
    public static void Release(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        lock (_cacheLock)
        {
            if (_cache.Remove(path, out var entry))
            {
                _lruList.Remove(entry.Node);
                Interlocked.Add(ref _cacheSizeBytes, -entry.Data.PcmData.Length);
                Console.WriteLine($"[AudioCache] Released '{path}'");
            }
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
        lock (_cacheLock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
        _pending.Clear();
        Interlocked.Exchange(ref _cacheSizeBytes, 0);
    }

    private static void EvictIfNeeded(long neededBytes)
    {
        while (true)
        {
            long current = Interlocked.Read(ref _cacheSizeBytes);
            long max = Interlocked.Read(ref _maxCacheSizeBytes);
            if (current + neededBytes <= max) break;

            string? evictKey;
            lock (_cacheLock)
            {
                var first = _lruList.First;
                if (first == null) break;
                evictKey = first.Value;
                if (_cache.Remove(evictKey, out var entry))
                {
                    _lruList.RemoveFirst();
                    Interlocked.Add(ref _cacheSizeBytes, -entry.Data.PcmData.Length);
                    Console.WriteLine($"[AudioCache] Evicted '{evictKey}' ({entry.Data.PcmData.Length / 1024:N0} KB)");
                }
            }
        }
    }
}
