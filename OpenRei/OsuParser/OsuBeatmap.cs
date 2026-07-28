using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRei.OsuParser.Models;
using OpenRei.Rhythm;

namespace OpenRei.OsuParser;

public class OsuBeatmap
{
    public Dictionary<string, string> General { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Editor { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Metadata { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Difficulty { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Events { get; } = new();
    public List<OsuTimingPoint> TimingPoints { get; } = new();
    public List<OsuHitObject> HitObjects { get; } = new();
    public ControlPointInfo ControlPoints { get; } = new();
    public int FormatVersion { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FolderPath => string.IsNullOrEmpty(FilePath) ? string.Empty : (System.IO.Path.GetDirectoryName(FilePath) ?? string.Empty);

    public IEnumerable<OsuNote> Notes => HitObjects.OfType<OsuNote>();
    public IEnumerable<OsuSlider> Sliders => HitObjects.OfType<OsuSlider>();
    public IEnumerable<OsuTimingPoint> BpmPoints => TimingPoints.Where(t => t.IsUninherited);

    public string GetGeneral(string key, string defaultValue = "")
        => General.TryGetValue(key, out var v) ? v : defaultValue;
    public string GetMeta(string key, string defaultValue = "")
        => Metadata.TryGetValue(key, out var v) ? v : defaultValue;
    public string GetDifficulty(string key, string defaultValue = "")
        => Difficulty.TryGetValue(key, out var v) ? v : defaultValue;
    public string GetEditor(string key, string defaultValue = "")
        => Editor.TryGetValue(key, out var v) ? v : defaultValue;

    public OsuTimingPoint? GetTimingPointAt(double t, bool uninheritedOnly = false)
    {
        OsuTimingPoint? result = null;
        foreach (var pt in TimingPoints)
        {
            if (pt.Time > t) break;
            if (!uninheritedOnly || pt.IsUninherited)
                result = pt;
        }

        if (result != null) return result;

        foreach (var pt in TimingPoints)
        {
            if (!uninheritedOnly || pt.IsUninherited)
                return pt;
        }

        return null;
    }

    public double GetBpmAt(double timeMs)
    {
        var pt = GetTimingPointAt(timeMs, uninheritedOnly: true);
        return pt?.BPM ?? double.NaN;
    }

    public void ResolveSliderVelocities()
    {
        double sliderMultiplier = 1.4;
        if (Difficulty.TryGetValue("SliderMultiplier", out var smStr)
            && double.TryParse(smStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double sm))
        {
            sliderMultiplier = Math.Clamp(sm, 0.4, 3.6);
        }

        foreach (var obj in HitObjects.OfType<OsuSlider>())
        {
            if (obj.ObjectType == HitObjectType.Hold) continue;

            var redLine = ControlPoints.TimingPointAt(obj.Time);
            double beatLengthMs = redLine.BeatLength;

            var diffPoint = ControlPoints.DifficultyPointAt(obj.Time);
            double velMult = diffPoint.SpeedMultiplier;

            double pixelsPerBeat = 100.0 * sliderMultiplier * velMult;
            double singlePassMs = (obj.Length / pixelsPerBeat) * beatLengthMs;

            if (singlePassMs <= 0 || double.IsNaN(singlePassMs) || double.IsInfinity(singlePassMs))
            {
                obj.DurationMs = 0;
                obj.EffectiveVelocityPxPerMs = 0;
            }
            else
            {
                obj.DurationMs = singlePassMs * obj.Slides;
                obj.EffectiveVelocityPxPerMs = obj.Length / singlePassMs;
            }
        }
    }

    public double GetSliderVelocityAt(double timeMs)
    {
        double sliderMultiplier = 1.4;
        if (Difficulty.TryGetValue("SliderMultiplier", out var smStr)
            && double.TryParse(smStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double sm))
        {
            sliderMultiplier = Math.Clamp(sm, 0.4, 3.6);
        }

        var redLine = ControlPoints.TimingPointAt(timeMs);
        var diffPoint = ControlPoints.DifficultyPointAt(timeMs);
        double velMult = diffPoint.SpeedMultiplier;

        return (100.0 * sliderMultiplier * velMult) / redLine.BeatLength;
    }

    public string Title         => GetMeta("Title");
    public string TitleUnicode  => GetMeta("TitleUnicode");
    public string Artist        => GetMeta("Artist");
    public string ArtistUnicode => GetMeta("ArtistUnicode");
    public string Creator       => GetMeta("Creator");
    public string Version       => GetMeta("Version");

    public int BeatmapId =>
        int.TryParse(GetMeta("BeatmapID"), out int id) ? id : 0;
    public int BeatmapSetId =>
        int.TryParse(GetMeta("BeatmapSetID"), out int id) ? id : 0;

    public string AudioFilename => GetGeneral("AudioFilename");
    public string AudioFullPath => string.IsNullOrEmpty(FolderPath) || string.IsNullOrEmpty(AudioFilename)
        ? string.Empty : System.IO.Path.Combine(FolderPath, AudioFilename);
    public int PreviewTime =>
        int.TryParse(GetGeneral("PreviewTime"), out int t) ? t : 0;
    public int Mode =>
        int.TryParse(GetGeneral("Mode"), out int m) ? m : 0;

    public string GetBackground()
    {
        foreach (var line in Events)
        {
            if (line.StartsWith("//") || line.StartsWith(" ")) continue;

            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            if (parts[0].Trim() != "0") continue;

            string filename = parts[2].Trim().Trim('"');
            if (filename.Length > 0)
                return filename;
        }

        return string.Empty;
    }

    public string GetBackgroundFullPath()
    {
        string bg = GetBackground();
        if (bg.Length == 0 || FilePath.Length == 0) return string.Empty;

        string? folder = System.IO.Path.GetDirectoryName(FilePath);
        return folder is null ? string.Empty : System.IO.Path.Combine(folder, bg);
    }

    public string GetVideo()
    {
        foreach (var line in Events)
        {
            if (line.StartsWith("//") || line.StartsWith(" ")) continue;

            var parts = line.Split(',');
            if (parts.Length < 3) continue;

            string type = parts[0].Trim();
            if (type != "1" && !string.Equals(type, "Video", StringComparison.OrdinalIgnoreCase)) continue;

            string filename = parts[2].Trim().Trim('"');
            if (filename.Length > 0)
                return filename;
        }

        return string.Empty;
    }

    public string GetVideoFullPath()
    {
        string vid = GetVideo();
        if (vid.Length == 0 || FilePath.Length == 0) return string.Empty;

        string? folder = System.IO.Path.GetDirectoryName(FilePath);
        return folder is null ? string.Empty : System.IO.Path.Combine(folder, vid);
    }

    public string VideoFullPath => string.IsNullOrEmpty(FolderPath) || string.IsNullOrEmpty(GetVideo())
        ? string.Empty : System.IO.Path.Combine(FolderPath, GetVideo());

    public double GetVideoOffsetMs()
    {
        foreach (var line in Events)
        {
            if (line.StartsWith("//") || line.StartsWith(" ")) continue;
            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            string type = parts[0].Trim();
            if (type != "1" && !string.Equals(type, "Video", StringComparison.OrdinalIgnoreCase)) continue;
            if (double.TryParse(parts[1].Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double ms))
                return ms;
        }
        return 0.0;
    }

    public override string ToString() =>
        $"[{Artist} - {Title}] {Version}  (ID:{BeatmapId})  {HitObjects.Count} objects";
}
