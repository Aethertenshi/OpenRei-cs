namespace OpenRei.OsuParser.Models;

public class OsuTimingPoint
{
    public double Time { get; set; }
    public double BeatLength { get; set; }
    public int Meter { get; set; }
    public int SampleSet { get; set; }
    public int SampleIndex { get; set; }
    public int Volume { get; set; }
    public bool IsUninherited { get; set; }
    public int Effects { get; set; }

    public double BPM => IsUninherited ? 60_000.0 / BeatLength : double.NaN;
    public double VelocityMultiplier => IsUninherited ? 1.0 : -100.0 / BeatLength;
    public bool IsKiai => (Effects & 1) != 0;

    public override string ToString() =>
        IsUninherited
            ? $"[Timing] t={Time}ms  BPM={BPM:F2}  Meter={Meter}"
            : $"[Inherited] t={Time}ms  Velocity={VelocityMultiplier:F2}";
}
