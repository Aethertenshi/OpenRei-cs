namespace reistar.Core;

using System;
using System.Collections.Generic;

public enum Easing
{
    Linear,
    Quadratic,
    Cubic,
    Quartic,
    Exponential,
    Sine,
    Quintic,
    Circular,
    Back,
    Fluid,
    Elastic
}

public enum EasingDirection
{
    In,
    Out,
    InOut
}

/// <summary>
/// A lightweight float tween that interpolates from a start value to an end value
/// over a specified duration using an easing function.
/// Automatically ticked every frame by the engine loop via <see cref="TickAll"/>.
/// </summary>
public sealed class Tween
{
    private static readonly List<Tween> _active = new();
    private static readonly List<Tween> _pendingAdd = new();

    private readonly Action<float> _onUpdate;
    private readonly Action? _onComplete;
    private float _startValue;
    private float _endValue;
    private readonly float _duration;
    private readonly Easing _easing;
    private readonly EasingDirection _direction;

    private float _elapsed;
    private bool _running;
    private bool _paused;
    private bool _inList;

    /// <summary>The most recent interpolated value produced by this tween.</summary>
    public float CurrentValue { get; private set; }

    /// <param name="startValue">Initial value when <c>t = 0</c>.</param>
    /// <param name="endValue">Final value when <c>t = 1</c>.</param>
    /// <param name="duration">Tween duration in seconds. Clamped to a minimum of 0.001.</param>
    /// <param name="onUpdate">Called every frame with the current interpolated value.</param>
    /// <param name="easing">Easing curve (default <see cref="Easing.Linear"/>).</param>
    /// <param name="direction">Ease-in, ease-out, or both (default <see cref="EasingDirection.In"/>).</param>
    /// <param name="onComplete">Optional callback invoked once when the tween finishes.</param>
    public Tween(float startValue, float endValue, float duration,
        Action<float> onUpdate,
        Easing easing = Easing.Linear,
        EasingDirection direction = EasingDirection.In,
        Action? onComplete = null)
    {
        _startValue = startValue;
        _endValue = endValue;
        _duration = Math.Max(duration, 0.001f);
        _onUpdate = onUpdate ?? throw new ArgumentNullException(nameof(onUpdate));
        _easing = easing;
        _direction = direction;
        _onComplete = onComplete;
        CurrentValue = startValue;
    }

    /// <summary>Starts the tween from the beginning.</summary>
    public void Start()
    {
        if (_running) return;
        _elapsed = 0f;
        _running = true;
        _paused = false;
        if (!_inList)
        {
            _pendingAdd.Add(this);
            _inList = true;
        }
    }

    /// <summary>
    /// Restarts the tween from its current value to its current end value.
    /// </summary>
    public void Restart() => Restart(CurrentValue, _endValue);

    /// <summary>
    /// Restarts the tween from its current value to a new end value.
    /// </summary>
    public void RestartTo(float newEndValue) => Restart(CurrentValue, newEndValue);

    /// <summary>
    /// Restarts the tween from an explicit start value to an explicit end value.
    /// </summary>
    public void Restart(float newStartValue, float newEndValue)
    {
        _startValue = newStartValue;
        _endValue = newEndValue;
        _elapsed = 0f;
        _running = true;
        _paused = false;
        if (!_inList)
        {
            _pendingAdd.Add(this);
            _inList = true;
        }
    }

    /// <summary>Stops the tween immediately without calling <c>onComplete</c>.</summary>
    public void Stop()
    {
        _running = false;
        _paused = false;
    }

    /// <summary>Pauses the tween at its current value.</summary>
    public void Pause()
    {
        _paused = true;
    }

    /// <summary>Resumes a paused tween.</summary>
    public void Resume()
    {
        _paused = false;
    }

    /// <summary>
    /// Ticks all active tweens. Called automatically by the framework each update frame.
    /// Zero allocations during execution.
    /// </summary>
    public static void TickAll(float dt)
    {
        if (_pendingAdd.Count > 0)
        {
            for (int i = 0; i < _pendingAdd.Count; i++)
            {
                _active.Add(_pendingAdd[i]);
            }
            _pendingAdd.Clear();
        }

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var tween = _active[i];
            if (tween._running && !tween._paused)
            {
                tween.Tick(dt);
            }

            if (!tween._running)
            {
                tween._inList = false;
                _active.RemoveAt(i);
            }
        }
    }

    private void Tick(float dt)
    {
        _elapsed += dt;

        float t = Math.Clamp(_elapsed / _duration, 0f, 1f);
        float eased = ApplyEasing(t, _easing, _direction);
        float value = _startValue + (_endValue - _startValue) * eased;
        CurrentValue = value;

        _onUpdate(value);

        if (t >= 1f)
        {
            _running = false;
            _onComplete?.Invoke();
        }
    }

    // ── easing math ──────────────────────────────────────────────────

    private static float ApplyEasing(float t, Easing easing, EasingDirection dir)
    {
        if (easing == Easing.Linear) return t;

        return dir switch
        {
            EasingDirection.In => EaseIn(t, easing),
            EasingDirection.Out => 1f - EaseIn(1f - t, easing),
            EasingDirection.InOut => t < 0.5f
                ? EaseIn(t * 2f, easing) * 0.5f
                : 1f - EaseIn((1f - t) * 2f, easing) * 0.5f,
            _ => t
        };
    }

    private static float EaseIn(float t, Easing easing)
    {
        return easing switch
        {
            Easing.Quadratic => t * t,
            Easing.Cubic => t * t * t,
            Easing.Quartic => t * t * t * t,
            Easing.Quintic => t * t * t * t * t,
            Easing.Fluid => t * t * t * t * t * t,
            Easing.Sine => 1f - MathF.Cos(t * MathF.PI * 0.5f),
            Easing.Circular => 1f - MathF.Sqrt(1f - Math.Clamp(t * t, 0f, 1f)),
            Easing.Exponential => ExponentialEaseIn(t),
            Easing.Back => BackEaseIn(t),
            Easing.Elastic => ElasticEaseIn(t),
            _ => t
        };
    }

    private static float ExponentialEaseIn(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        const float bias = 0.0009765625f;
        return (MathF.Pow(2f, 10f * (t - 1f)) - bias) / (1f - bias);
    }

    private static float BackEaseIn(float t)
    {
        const float s = 1.70158f;
        return t * t * ((s + 1f) * t - s);
    }

    private static float ElasticEaseIn(float t)
    {
        if (t == 0f) return 0f;
        if (t >= 1f) return 1f;
        const float p = 0.3f;
        return -MathF.Pow(2f, 10f * (t - 1f)) * MathF.Sin((t - p * 0.25f) * (2f * MathF.PI) / p);
    }
}
