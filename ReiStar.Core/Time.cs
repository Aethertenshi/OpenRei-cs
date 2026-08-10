namespace reistar.Core;

using System;
using SDL;

/// <summary>
/// Layer 2 Features Engine: High-precision frame timing math, rolling window FPS counter, TargetFPS throttling, and time scale control.
/// </summary>
public static class Time
{
    private const int SampleCount = 120; // 120-frame rolling window for smooth, frame-perfect FPS calculation
    private static readonly float[] _frameSamples = new float[SampleCount];
    private static int _sampleIndex = 0;
    private static int _recordedSamples = 0;
    private static float _accumulatedTime = 0f;

    /// <summary>
    /// Target frame rate cap (e.g. 60, 144, 240). Set to 0 for uncapped rendering.
    /// </summary>
    public static int TargetFPS { get; set; } = 0;

    /// <summary>
    /// Frame delta time in seconds, scaled by <see cref="TimeScale"/>.
    /// </summary>
    public static float DeltaTime { get; private set; }

    /// <summary>
    /// Raw unscaled frame delta time in seconds.
    /// </summary>
    public static float UnscaledDeltaTime { get; private set; }

    /// <summary>
    /// Multiplier for <see cref="DeltaTime"/> (1.0 = normal speed, 0.5 = slow-motion, 2.0 = fast-forward).
    /// </summary>
    public static float TimeScale { get; set; } = 1.0f;

    /// <summary>
    /// Total elapsed engine runtime in seconds.
    /// </summary>
    public static double TotalSeconds { get; private set; }

    /// <summary>
    /// Total number of frames rendered since startup.
    /// </summary>
    public static ulong FrameCount { get; private set; }

    /// <summary>
    /// Gets the instantaneous FPS based on the latest single frame delta.
    /// </summary>
    public static float InstantaneousFPS => UnscaledDeltaTime > 0f ? (1.0f / UnscaledDeltaTime) : 0f;

    /// <summary>
    /// Gets the frame-perfect average FPS calculated over a high-precision 120-frame rolling window.
    /// </summary>
    public static float GetFPS()
    {
        if (_recordedSamples == 0 || _accumulatedTime <= 0f) return 0f;
        return _recordedSamples / _accumulatedTime;
    }

    /// <summary>
    /// Updates high-resolution frame timing calculations.
    /// </summary>
    public static void Update(ulong deltaTicks, ulong frequency)
    {
        FrameCount++;

        double rawDelta = (double)deltaTicks / frequency;
        // Clamp raw delta time to 0.1s max to prevent physics explosions on pause/hitch
        rawDelta = Math.Min(rawDelta, 0.1);

        UnscaledDeltaTime = (float)rawDelta;
        DeltaTime = UnscaledDeltaTime * TimeScale;
        TotalSeconds += rawDelta;

        // Maintain rolling 120-frame sample buffer for frame-perfect FPS calculation
        if (_recordedSamples == SampleCount)
        {
            _accumulatedTime -= _frameSamples[_sampleIndex];
        }
        else
        {
            _recordedSamples++;
        }

        _frameSamples[_sampleIndex] = UnscaledDeltaTime;
        _accumulatedTime += UnscaledDeltaTime;
        _sampleIndex = (_sampleIndex + 1) % SampleCount;
    }

    /// <summary>
    /// Throttles high-resolution nanosecond execution to maintain the target FPS frame budget.
    /// </summary>
    public static void ThrottleFrame(ulong frameStartCounter, ulong currentCounter, ulong frequency)
    {
        if (TargetFPS <= 0 || frequency == 0) return;

        double targetFrameTimeSeconds = 1.0 / TargetFPS;
        ulong targetTicks = (ulong)(targetFrameTimeSeconds * frequency);
        ulong elapsedTicks = currentCounter - frameStartCounter;

        if (elapsedTicks < targetTicks)
        {
            ulong remainingTicks = targetTicks - elapsedTicks;
            double remainingSeconds = (double)remainingTicks / frequency;
            ulong remainingNanoseconds = (ulong)(remainingSeconds * 1_000_000_000.0);

            if (remainingNanoseconds > 0)
            {
                SDL3.SDL_DelayNS(remainingNanoseconds);
            }
        }
    }
}
