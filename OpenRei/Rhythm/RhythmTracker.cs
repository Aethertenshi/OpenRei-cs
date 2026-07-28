namespace OpenRei.Rhythm;

/// <summary>
/// Tracks beat progress using osu!-style timing control points.
/// </summary>
public sealed class RhythmTracker
{
    public int CurrentBeatIndex { get; private set; }
    public float BeatProgress { get; private set; }
    public bool IsDownbeat { get; private set; }

    /// <summary>Fires when a new beat starts (CurrentBeatIndex increments).</summary>
    public event Action<int>? OnBeat;
    /// <summary>Fires on the first beat of each measure.</summary>
    public event Action? OnDownBeat;

    private int _lastBeatIndex = -1;

    public void Update(float musicTimeMs, ControlPointInfo controlPoints)
    {
        TimingControlPoint? activeTiming = null;
        int priorWholeBeats = 0;

        foreach (var tp in controlPoints.TimingPoints)
        {
            if (tp.Time > musicTimeMs) break;
            if (activeTiming != null)
            {
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
        if (beatsSinceTimingPoint < 0f) beatsSinceTimingPoint = 0f;

        int localBeatIndex = (int)Math.Floor(beatsSinceTimingPoint);
        CurrentBeatIndex = priorWholeBeats + localBeatIndex;
        BeatProgress = beatsSinceTimingPoint - localBeatIndex;

        int meter = activeTiming.Meter > 0 ? activeTiming.Meter : 4;
        IsDownbeat = localBeatIndex % meter == 0;

        // Fire events on beat transition
        if (CurrentBeatIndex != _lastBeatIndex)
        {
            OnBeat?.Invoke(CurrentBeatIndex);
            if (IsDownbeat)
                OnDownBeat?.Invoke();
            _lastBeatIndex = CurrentBeatIndex;
        }
    }

    public void Reset()
    {
        CurrentBeatIndex = 0;
        BeatProgress = 0f;
        IsDownbeat = false;
        _lastBeatIndex = -1;
    }
}