using System.IO.Compression;

namespace OpenRei.IO;

/// <summary>
/// Extracts an .osz file (a ZIP archive containing beatmaps, audio, and hitsounds) into a uniquely-named subfolder.
/// </summary>
public static class OszImporter
{
    /// <summary>
    /// Imports an .osz file into songsPath directory.
    /// </summary>
    /// <param name="oszPath">Full path to the .osz file.</param>
    /// <param name="songsPath">Root songs directory.</param>
    /// <returns>The full path of the extracted folder, or null on failure.</returns>
    public static string? Import(string oszPath, string songsPath)
    {
        if (!File.Exists(oszPath))
        {
            Console.WriteLine($"[OszImporter] File not found: {oszPath}");
            return null;
        }

        if (!IsOszFile(oszPath))
        {
            Console.WriteLine($"[OszImporter] Not an .osz file: {oszPath}");
            return null;
        }

        Directory.CreateDirectory(songsPath);

        string destFolder;
        do
        {
            destFolder = Path.Combine(songsPath, Guid.NewGuid().ToString("N")[..16]);
        }
        while (Directory.Exists(destFolder));

        try
        {
            Directory.CreateDirectory(destFolder);
            ZipFile.ExtractToDirectory(oszPath, destFolder, overwriteFiles: true);
            Console.WriteLine($"[OszImporter] Extracted → {destFolder}");
            return destFolder;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OszImporter] Extraction failed: {ex.Message}");

            try
            {
                if (Directory.Exists(destFolder))
                    Directory.Delete(destFolder, recursive: true);
            }
            catch { /* best effort cleanup */ }

            return null;
        }
    }

    /// <summary>
    /// Returns true if the path looks like a valid .osz file.
    /// </summary>
    public static bool IsOszFile(string path) =>
        !string.IsNullOrEmpty(path) &&
        File.Exists(path) &&
        Path.GetExtension(path).Equals(".osz", StringComparison.OrdinalIgnoreCase);
}
