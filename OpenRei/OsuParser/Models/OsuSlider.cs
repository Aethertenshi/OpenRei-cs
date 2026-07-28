using System.Collections.Generic;

namespace OpenRei.OsuParser.Models;

public enum SliderCurveType
{
    Bezier,
    CatmullRom,
    Linear,
    PerfectCircle,
    Unknown
}

public struct SliderPoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public override string ToString() => $"({X},{Y})";
}

public class OsuSlider : OsuHitObject
{
    public SliderCurveType CurveType { get; set; }
    public List<SliderPoint> CurvePoints { get; set; } = new();
    public int Slides { get; set; }
    public double Length { get; set; }
    public List<int> EdgeSounds { get; set; } = new();
    public List<string> EdgeSets { get; set; } = new();
    public double EffectiveVelocityPxPerMs { get; set; }
    public double DurationMs { get; set; }
    public double EndTime => Time + DurationMs;

    public OsuSlider()
    {
        ObjectType = HitObjectType.Slider;
    }

    public override string ToString() =>
        $"Slider @ {Time}ms  ({X},{Y})  len={Length}px  slides={Slides}  dur={DurationMs:F0}ms";
}
