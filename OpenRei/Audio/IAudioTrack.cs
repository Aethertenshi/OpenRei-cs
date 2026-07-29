namespace OpenRei.Audio;

/// <summary>
/// Common interface for playable audio sources, implemented by
/// <see cref="AudioStream"/> (full PCM, sub-ms seek) and
/// <see cref="MusicTrack"/> (streaming, instant start).
/// </summary>
public interface IAudioTrack
{
    float Volume { get; set; }
    bool IsPlaying { get; }
    double PositionMs { get; set; }
    void Play();
    void Pause();
    void Stop();
}
