using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenRei.OsuParser;

public class OsuScanner
{
    private readonly OsuParser _parser = new();

    public IReadOnlyList<string> FindOsuFiles(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {rootDirectory}");

        var osuFiles = new List<string>();
        ScanDirectoryRecursive(rootDirectory, osuFiles);
        return osuFiles;
    }

    private void ScanDirectoryRecursive(string path, List<string> results)
    {
        try
        {
            var files = Directory.GetFiles(path, "*.osu");
            results.AddRange(files);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OsuScanner] Error getting files in {path}: {ex.Message}");
        }

        try
        {
            var subDirs = Directory.GetDirectories(path);
            foreach (var dir in subDirs)
                ScanDirectoryRecursive(dir, results);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OsuScanner] Error enumerating subdirectories in {path}: {ex.Message}");
        }
    }

    public IReadOnlyList<OsuBeatmap> ScanAll(
        string rootDirectory,
        Action<string, Exception>? onError = null,
        bool metadataOnly = false)
    {
        var paths = FindOsuFiles(rootDirectory);
        var results = new List<OsuBeatmap>(paths.Count);

        foreach (var path in paths)
        {
            try
            {
                results.Add(_parser.Parse(path, metadataOnly));
            }
            catch (Exception ex)
            {
                onError?.Invoke(path, ex);
            }
        }

        return results;
    }

    public IEnumerable<OsuBeatmap> ScanLazy(
        string rootDirectory,
        Action<string, Exception>? onError = null,
        bool metadataOnly = false)
    {
        var paths = FindOsuFiles(rootDirectory);

        foreach (var path in paths)
        {
            OsuBeatmap? bm = null;
            try
            {
                bm = _parser.Parse(path, metadataOnly);
            }
            catch (Exception ex)
            {
                onError?.Invoke(path, ex);
            }

            if (bm != null) yield return bm;
        }
    }

    /// <summary>
    /// Scans a directory tree on a background thread and returns all parsed beatmaps.
    /// Backwards-compatible async equivalent of <see cref="ScanAll"/>.
    /// </summary>
    public Task<IReadOnlyList<OsuBeatmap>> ScanAllAsync(
        string rootDirectory,
        Action<string, Exception>? onError = null,
        bool metadataOnly = false)
    {
        return Task.Run(() => ScanAll(rootDirectory, onError, metadataOnly));
    }

    public IReadOnlyList<OsuBeatmap> ScanFiltered(
        string rootDirectory,
        Func<OsuBeatmap, bool> filter,
        Action<string, Exception>? onError = null,
        bool metadataOnly = false)
    {
        return ScanLazy(rootDirectory, onError, metadataOnly)
            .Where(filter)
            .ToList();
    }

    public IReadOnlyList<OsuBeatmap> ParseSet(string beatmapSetDirectory, bool metadataOnly = false)
    {
        if (!Directory.Exists(beatmapSetDirectory))
            throw new DirectoryNotFoundException(beatmapSetDirectory);

        var results = new List<OsuBeatmap>();
        foreach (var f in Directory.GetFiles(beatmapSetDirectory, "*.osu"))
        {
            try { results.Add(_parser.Parse(f, metadataOnly)); }
            catch { }
        }
        return results;
    }
}
