namespace OpenRei.OsuParser.Models;

public enum HitObjectType
{
    Note,
    Slider,
    Spinner,
    Hold,
    Unknown
}

public abstract class OsuHitObject
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Time { get; set; }
    public int TypeRaw { get; set; }
    public HitObjectType ObjectType { get; set; }
    public bool IsNewCombo { get; set; }
    public int ComboSkip { get; set; }
    public int HitSound { get; set; }
    public string HitSample { get; set; } = string.Empty;

    public override string ToString() =>
        $"{ObjectType} @ {Time}ms  ({X},{Y})";
}
