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

    /// <summary>
    /// Fills the buffer with real-time frequency magnitude spectrum bins (e.g. 256 or 512 bins).
    /// </summary>
    void GetFftData(Span<float> fftBuffer);

    /// <summary>
    /// Returns the current peak audio level (0.0 to 1.0).
    /// </summary>
    float GetLevel();
}
