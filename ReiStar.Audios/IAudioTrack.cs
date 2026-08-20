namespace reistar.Audios;

using System;

public interface IAudioTrack : IDisposable
{
    float Volume { get; set; }
    float Pitch { get; set; }
    float PlaybackSpeed { get; set; }
    bool Loop { get; set; }
    bool IsPlaying { get; }
    bool IsDisposed { get; }
    double Position { get; set; }
    double PositionMs { get; set; }
    double Length { get; }
    double LengthMs { get; }
    Action? OnPlaying { get; set; }

    void Play();
    void Pause();
    void Stop();
    void Seek(double positionMs);
}
