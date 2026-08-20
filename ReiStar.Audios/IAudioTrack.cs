namespace reistar.Audios;

using System;

public interface IAudioTrack : IDisposable
{
    float Volume { get; set; }
    float Pitch { get; set; }
    bool Loop { get; set; }
    bool IsPlaying { get; }
    double Position { get; set; }
    double PositionMs { get; set; }
    double Length { get; }
    double LengthMs { get; }

    void Play();
    void Pause();
    void Stop();
    void Seek(double positionMs);
}
