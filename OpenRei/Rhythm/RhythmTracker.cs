namespace OpenRei.Rhythm;

/// <summary>
/// Tracks beat progress using osu!-style timing control points.
/// </summary>
public sealed class RhythmTracker
{
    /// <summary>
    /// A strictly increasing integer representing the current beat. Safe to use for trigger checks (Current > Last).
    /// </summary>
    public int CurrentBeatIndex { get; private set; }

    /// <summary>
    /// A value between 0.0 and 1.0 representing the progress of the current beat.
    /// </summary>
    public float BeatProgress { get; private set; }

    /// <summary>
    /// True if the current beat is the first beat of the measure (the downbeat).
    /// </summary>
    public bool IsDownbeat { get; private set; }

    public void Update(float musicTimeMs, ControlPointInfo controlPoints)
    {
        TimingControlPoint? activeTiming = null;
        int priorWholeBeats = 0;

        foreach (var tp in controlPoints.TimingPoints)
        {
            if (tp.Time > musicTimeMs)
                break;

            if (activeTiming != null)
            {
                // A new timing point snaps the metronome; count whole beats in the previous section.
                float sectionDurationMs = (float)(tp.Time - activeTiming.Time);
                priorWholeBeats += (int)Math.Floor(sectionDurationMs / activeTiming.BeatLength);
            }

            activeTiming = tp;
        }

        if (activeTiming == null)
        {
            CurrentBeatIndex = 0;
            BeatProgress = 0f;
            IsDownbeat = false;
            return;
        }

        float msSinceTimingPoint = musicTimeMs - (float)activeTiming.Time;
        float beatsSinceTimingPoint = msSinceTimingPoint / (float)activeTiming.BeatLength;

        if (beatsSinceTimingPoint < 0f)
            beatsSinceTimingPoint = 0f;

        int localBeatIndex = (int)Math.Floor(beatsSinceTimingPoint);

        CurrentBeatIndex = priorWholeBeats + localBeatIndex;
        BeatProgress = beatsSinceTimingPoint - localBeatIndex;

        int meter = activeTiming.Meter > 0 ? activeTiming.Meter : 4;
        IsDownbeat = localBeatIndex % meter == 0;
    }

    /// <summary>
    /// Resets the tracker to its initial state.
    /// </summary>
    public void Reset()
    {
        CurrentBeatIndex = 0;
        BeatProgress = 0f;
        IsDownbeat = false;
    }
}
