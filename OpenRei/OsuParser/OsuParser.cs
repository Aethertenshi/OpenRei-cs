using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenRei.OsuParser.Models;
using OpenRei.Rhythm;

namespace OpenRei.OsuParser;

public class OsuParser
{
    private const int TYPE_CIRCLE   = 1 << 0;
    private const int TYPE_SLIDER   = 1 << 1;
    private const int TYPE_NEWCOMBO = 1 << 2;
    private const int TYPE_SPINNER  = 1 << 3;
    private const int COMBO_SKIP    = 0b0111_0000;
    private const int TYPE_HOLD     = 1 << 7;

    private const int EARLY_VERSION_TIMING_OFFSET = 24;

    public OsuBeatmap Parse(string path, bool metadataOnly = false)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("File not found", path);

        if (metadataOnly)
        {
            // All metadata lives in [General]..[TimingPoints]. Stream-read only those
            // sections and stop before [Colours]/[HitObjects] to avoid full-file I/O
            // and allocation on large .osu files (storyboards, hit objects).
            // Produces identical data to a full read for metadata consumers.
            var lines = new List<string>(64);
            using (var reader = new StreamReader(path))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string t = line.Trim();
                    if (t.StartsWith("[") && t.EndsWith("]") &&
                        (t == "[Colours]" || t == "[HitObjects]"))
                    {
                        break;
                    }
                    lines.Add(line);
                }
            }
            return ParseLines(lines.ToArray(), path, true);
        }

        var allLines = File.ReadAllLines(path);
        return ParseLines(allLines, path, false);
    }

    public OsuBeatmap ParseText(string content, string sourcePath = "", bool metadataOnly = false)
    {
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        return ParseLines(lines, sourcePath, metadataOnly);
    }

    private OsuBeatmap ParseLines(string[] lines, string filePath, bool metadataOnly = false)
    {
        var beatmap = new OsuBeatmap { FilePath = filePath };

        if (lines.Length > 0 && lines[0].StartsWith("osu file format v"))
        {
            if (int.TryParse(lines[0].Replace("osu file format v", "").Trim(), out int ver))
                beatmap.FormatVersion = ver;
        }

        double offset = beatmap.FormatVersion < 5 ? EARLY_VERSION_TIMING_OFFSET : 0;

        string currentSection = "";

        for (int i = 1; i < lines.Length; i++)
        {
            string raw  = lines[i];
            string line = raw.Trim();

            if (line.Length == 0 || line.StartsWith("//")) continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line[1..^1];

                if (metadataOnly && (currentSection == "Colours" || currentSection == "HitObjects"))
                    break;
                continue;
            }

            switch (currentSection)
            {
                case "General":
                case "Editor":
                case "Metadata":
                case "Difficulty":
                    ParseKeyValue(line, currentSection, beatmap);
                    break;

                case "Events":
                    beatmap.Events.Add(line);
                    break;

                case "TimingPoints":
                    var tp = ParseTimingPoint(line, offset);
                    if (tp != null)
                    {
                        beatmap.TimingPoints.Add(tp);

                        if (tp.IsUninherited)
                        {
                            beatmap.ControlPoints.TimingPoints.Add(new TimingControlPoint
                            {
                                Time = tp.Time,
                                BeatLength = tp.BeatLength,
                                Meter = tp.Meter
                            });

                            beatmap.ControlPoints.DifficultyPoints.Add(new DifficultyControlPoint
                            {
                                Time = tp.Time,
                                SpeedMultiplier = 1.0
                            });
                        }
                        else
                        {
                            beatmap.ControlPoints.DifficultyPoints.Add(new DifficultyControlPoint
                            {
                                Time = tp.Time,
                                SpeedMultiplier = tp.VelocityMultiplier
                            });
                        }

                        beatmap.ControlPoints.SoundPoints.Add(new SoundControlPoint
                        {
                            Time = tp.Time,
                            Volume = tp.Volume,
                            SampleSet = tp.SampleSet,
                            SampleIndex = tp.SampleIndex
                        });

                        beatmap.ControlPoints.EffectPoints.Add(new EffectControlPoint
                        {
                            Time = tp.Time,
                            IsKiai = tp.IsKiai,
                            OmitFirstBarLine = (tp.Effects & 8) != 0
                        });
                    }
                    break;

                case "HitObjects":
                    var obj = ParseHitObject(line, offset);
                    if (obj != null) beatmap.HitObjects.Add(obj);
                    break;
            }
        }

        beatmap.TimingPoints.Sort((a, b) => a.Time.CompareTo(b.Time));

        beatmap.ControlPoints.TimingPoints.Sort((a, b) => a.Time.CompareTo(b.Time));
        beatmap.ControlPoints.DifficultyPoints.Sort((a, b) => a.Time.CompareTo(b.Time));
        beatmap.ControlPoints.SoundPoints.Sort((a, b) => a.Time.CompareTo(b.Time));
        beatmap.ControlPoints.EffectPoints.Sort((a, b) => a.Time.CompareTo(b.Time));

        if (!metadataOnly)
        {
            beatmap.HitObjects.Sort((a, b) => a.Time.CompareTo(b.Time));
            beatmap.ResolveSliderVelocities();
        }

        return beatmap;
    }

    private static void ParseKeyValue(string line, string section, OsuBeatmap beatmap)
    {
        int colonIdx = line.IndexOf(':');
        if (colonIdx < 0) return;

        string key   = line[..colonIdx].Trim();
        string value = line[(colonIdx + 1)..].Trim();

        switch (section)
        {
            case "General":    beatmap.General[key]    = value; break;
            case "Editor":     beatmap.Editor[key]     = value; break;
            case "Metadata":   beatmap.Metadata[key]   = value; break;
            case "Difficulty": beatmap.Difficulty[key] = value; break;
        }
    }

    private static OsuTimingPoint? ParseTimingPoint(string line, double offset)
    {
        var parts = line.Split(',');
        if (parts.Length < 2) return null;

        var tp = new OsuTimingPoint();

        if (TryParseDouble(parts, 0, out double t))
            tp.Time = Math.Floor(t) + offset;

        if (TryParseDouble(parts, 1, out double bl))  tp.BeatLength = bl;
        if (TryParseInt(parts, 2, out int meter))     tp.Meter      = meter;
        if (TryParseInt(parts, 3, out int ss))        tp.SampleSet  = ss;
        if (TryParseInt(parts, 4, out int si))        tp.SampleIndex = si;
        if (TryParseInt(parts, 5, out int vol))       tp.Volume     = vol;
        if (TryParseInt(parts, 6, out int uninh))     tp.IsUninherited = uninh == 1;
        if (TryParseInt(parts, 7, out int fx))        tp.Effects    = fx;

        if (tp.IsUninherited)
        {
            tp.BeatLength = Math.Clamp(tp.BeatLength, 6.0, 60000.0);
        }
        else
        {
            if (tp.BeatLength >= 0)
            {
                tp.BeatLength = -100.0;
            }
            else
            {
                tp.BeatLength = Math.Clamp(tp.BeatLength, -1000.0, -10.0);
            }
        }

        return tp;
    }

    private static OsuHitObject? ParseHitObject(string line, double offset)
    {
        var parts = line.Split(',');
        if (parts.Length < 5) return null;

        if (!TryParseInt(parts, 0, out int x))    return null;
        if (!TryParseInt(parts, 1, out int y))    return null;
        if (!TryParseInt(parts, 2, out int time)) return null;
        if (!TryParseInt(parts, 3, out int type)) return null;
        if (!TryParseInt(parts, 4, out int hs))   return null;

        double startTime = time + offset;
        int adjustedTime = (int)Math.Round(startTime);

        bool isNewCombo  = (type & TYPE_NEWCOMBO) != 0;
        int  comboSkip   = (type & COMBO_SKIP) >> 4;

        OsuHitObject obj;

        if ((type & TYPE_HOLD) != 0)
        {
            obj = ParseHold(parts, 5, startTime, offset);
        }
        else if ((type & TYPE_SLIDER) != 0)
        {
            obj = ParseSlider(parts, 5);
        }
        else if ((type & TYPE_SPINNER) != 0)
        {
            obj = ParseSpinner(parts, 5, startTime, offset);
        }
        else if ((type & TYPE_CIRCLE) != 0)
        {
            var note = new OsuNote();
            if (parts.Length > 5)
                note.HitSample = parts[5].Trim();
            obj = note;
        }
        else
        {
            obj = new OsuNote { ObjectType = HitObjectType.Unknown };
        }

        obj.X          = x;
        obj.Y          = y;
        obj.Time       = adjustedTime;
        obj.TypeRaw    = type;
        obj.HitSound   = hs;
        obj.IsNewCombo = isNewCombo;
        obj.ComboSkip  = comboSkip;

        return obj;
    }

    private static OsuNote ParseSpinner(string[] parts, int paramsIdx, double startTime, double offset)
    {
        var spinner = new OsuNote { ObjectType = HitObjectType.Spinner };

        if (paramsIdx < parts.Length && TryParseDouble(parts, paramsIdx, out double endTime))
        {
            spinner.DurationMs = Math.Max(0, (endTime + offset) - startTime);
        }

        if (paramsIdx + 1 < parts.Length)
            spinner.HitSample = parts[paramsIdx + 1].Trim();

        return spinner;
    }

    private static OsuSlider ParseSlider(string[] parts, int paramsIdx)
    {
        var s = new OsuSlider();
        if (paramsIdx >= parts.Length) return s;

        string curveStr = parts[paramsIdx];
        var curveParts  = curveStr.Split('|');

        s.CurveType = curveParts[0] switch
        {
            "B" => SliderCurveType.Bezier,
            "C" => SliderCurveType.CatmullRom,
            "L" => SliderCurveType.Linear,
            "P" => SliderCurveType.PerfectCircle,
            _   => SliderCurveType.Unknown
        };

        for (int i = 1; i < curveParts.Length; i++)
        {
            var xy = curveParts[i].Split(':');
            if (xy.Length == 2
                && int.TryParse(xy[0], out int cx)
                && int.TryParse(xy[1], out int cy))
            {
                s.CurvePoints.Add(new SliderPoint { X = cx, Y = cy });
            }
        }

        if (TryParseInt(parts, paramsIdx + 1, out int slides))
            s.Slides = Math.Max(1, slides);

        if (TryParseDouble(parts, paramsIdx + 2, out double len))
            s.Length = Math.Max(0, len);

        if (paramsIdx + 3 < parts.Length && parts[paramsIdx + 3].Trim().Length > 0)
        {
            foreach (var es in parts[paramsIdx + 3].Split('|'))
                if (int.TryParse(es, out int esv)) s.EdgeSounds.Add(esv);
        }

        if (paramsIdx + 4 < parts.Length && parts[paramsIdx + 4].Trim().Length > 0)
        {
            foreach (var eSet in parts[paramsIdx + 4].Split('|'))
                s.EdgeSets.Add(eSet);
        }

        if (paramsIdx + 5 < parts.Length)
            s.HitSample = parts[paramsIdx + 5].Trim();

        return s;
    }

    private static OsuSlider ParseHold(string[] parts, int paramsIdx, double startTime, double offset)
    {
        var s = new OsuSlider { ObjectType = HitObjectType.Hold };
        if (paramsIdx >= parts.Length) return s;

        string paramStr = parts[paramsIdx];

        if (!string.IsNullOrEmpty(paramStr))
        {
            var holdParams = paramStr.Split(':');

            if (TryParseDouble(holdParams, 0, out double endTime))
            {
                endTime = Math.Max(startTime, endTime + offset);
                s.DurationMs = endTime - startTime;
            }

            if (holdParams.Length > 1)
            {
                s.HitSample = string.Join(':', holdParams, 1, holdParams.Length - 1);
            }
        }

        s.Slides = 1;
        return s;
    }

    private static bool TryParseInt(string[] parts, int idx, out int value)
    {
        value = 0;
        return idx < parts.Length
            && int.TryParse(parts[idx].Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
    }

    private static bool TryParseDouble(string[] parts, int idx, out double value)
    {
        value = 0;
        return idx < parts.Length
            && double.TryParse(parts[idx].Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
    }
}
