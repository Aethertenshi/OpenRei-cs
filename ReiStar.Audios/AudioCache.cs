namespace reistar.Audios;

using System;
using System.Collections.Concurrent;

public static class AudioCache
{
    private static readonly ConcurrentDictionary<string, SoundEffect> _soundCache = new();
    private static readonly ConcurrentDictionary<string, DecodedAudioData> _decodedCache = new();

    public static SoundEffect GetSound(string filePath, int poolSize = 8)
    {
        return _soundCache.GetOrAdd(filePath, path => new SoundEffect(path, poolSize));
    }

    public static DecodedAudioData GetDecodedData(string filePath)
    {
        return _decodedCache.GetOrAdd(filePath, path => AudioDecoder.DecodeFile(path));
    }

    public static void Clear()
    {
        foreach (var sfx in _soundCache.Values)
        {
            sfx.Dispose();
        }
        _soundCache.Clear();
        _decodedCache.Clear();
    }
}
