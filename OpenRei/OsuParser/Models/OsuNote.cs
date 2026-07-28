namespace OpenRei.OsuParser.Models;

public class OsuNote : OsuHitObject
{
    public double DurationMs { get; set; }
    public double EndTime => Time + DurationMs;

    public OsuNote()
    {
        ObjectType = HitObjectType.Note;
    }
}
