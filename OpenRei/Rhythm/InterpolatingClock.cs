using System.Diagnostics;
using OpenRei.Audio;

namespace OpenRei.Rhythm;

/// <summary>
/// A gameplay clock that interpolates between audio position samples for smooth, low-jitter timing.
/// </summary>
public sealed class InterpolatingAudioClock
{
    private readonly Func<double> _getSourceTime;
    private readonly Stopwatch _frameStopwatch = new();

    private double _currentTime;
    private double _rate = 1.0;
    private bool _isRunning;

    /// <param name="getSourceTime">Returns the authoritative audio position in milliseconds.</param>
    public InterpolatingAudioClock(Func<double> getSourceTime)
    {
        _getSourceTime = getSourceTime ?? throw new ArgumentNullException(nameof(getSourceTime));
    }

    /// <param name="stream">AudioStream whose PositionMs will be used as the authoritative time source.</param>
    public InterpolatingAudioClock(AudioStream stream)
        : this(() => stream?.PositionMs ?? 0.0)
    {
        ArgumentNullException.ThrowIfNull(stream);
    }

    /// <summary>Current interpolated time in milliseconds.</summary>
    public double CurrentTime => _currentTime;

    /// <summary>Current playback rate used for interpolation.</summary>
    public double Rate => _rate;

    /// <summary>Whether the clock is running.</summary>
    public bool IsRunning => _isRunning;

    public void Start()
    {
        _frameStopwatch.Restart();
        _currentTime = _getSourceTime();
        _rate = 1.0;
        _isRunning = true;
    }

    public void Stop()
    {
        _isRunning = false;
        _frameStopwatch.Stop();
    }

    public void Reset()
    {
        _currentTime = 0.0;
        _rate = 1.0;
        _isRunning = false;
        _frameStopwatch.Reset();
    }

    public void Seek(double time)
    {
        _currentTime = time;
        _frameStopwatch.Restart();
    }

    /// <summary>
    /// Advances the clock. Should be called once per update frame.
    /// </summary>
    public void Update()
    {
        if (!_isRunning)
            return;

        double source = _getSourceTime();
        double elapsed = _frameStopwatch.Elapsed.TotalMilliseconds;
        _frameStopwatch.Restart();

        _currentTime += elapsed * _rate;

        const double maxDriftMs = 25.0;
        if (Math.Abs(_currentTime - source) > maxDriftMs)
        {
            // The interpolated clock has drifted too far from the audio source; snap back.
            _currentTime = source;
            _rate = 1.0;
        }
        else
        {
            // Gently nudge the interpolation rate so the clock converges to the source.
            double drift = source - _currentTime;
            _rate = 1.0 + drift / Math.Max(elapsed, 1.0) * 0.05;
            _rate = Math.Clamp(_rate, 0.5, 2.0);
        }
    }
}
