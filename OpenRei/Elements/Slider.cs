using OpenRei.Graphics;
using OpenRei.Tween;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// A 1:1 osu!-style interactive settings slider with smooth drag scrubbing, rounded pill tracks,
/// accent progress fills, hover/grab micro-animations, drop shadows, and step precision snapping.
/// </summary>
public class Slider : Element
{
    private float _value = 0.5f;
    private float _min = 0f;
    private float _max = 1f;
    private float _step = 0f; // 0 = continuous

    private bool _isDragging;
    private bool _isHovered;
    private float _knobScale = 1.0f;
    private Tween.Tween? _knobTween;

    // ── Properties ─────────────────────────────────────────────────────────────

    /// <summary>Current slider value between <see cref="Min"/> and <see cref="Max"/>.</summary>
    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, _min, _max);
            if (_step > 0f)
            {
                clamped = MathF.Round((clamped - _min) / _step) * _step + _min;
                clamped = Math.Clamp(clamped, _min, _max);
            }

            if (Math.Abs(clamped - _value) < 0.00001f) return;
            _value = clamped;
            OnValueChanged?.Invoke(_value);
            UpdateValueLabel();
        }
    }

    /// <summary>Normalized value between 0.0 and 1.0.</summary>
    public float NormalizedValue
    {
        get => (_max > _min) ? (_value - _min) / (_max - _min) : 0f;
        set => Value = _min + Math.Clamp(value, 0f, 1f) * (_max - _min);
    }

    public float Min
    {
        get => _min;
        set
        {
            _min = value;
            Value = _value;
        }
    }

    public float Max
    {
        get => _max;
        set
        {
            _max = value;
            Value = _value;
        }
    }

    public float Step
    {
        get => _step;
        set => _step = MathF.Max(0f, value);
    }

    // ── Visual Styling ────────────────────────────────────────────────────────

    public Color TrackColor { get; set; } = Color.FromRgba(25, 27, 34, 255);
    public Color FillColor { get; set; } = Color.FromRgba(102, 204, 255, 255); // osu! Cyan Blue
    public Color KnobColor { get; set; } = Color.White;
    public Color KnobHoverColor { get; set; } = Color.FromRgba(240, 240, 255, 255);
    public Color KnobBorderColor { get; set; } = Color.FromRgba(255, 255, 255, 200);

    public float TrackHeight { get; set; } = 12f; // Height of the track bar
    public float KnobWidth { get; set; } = 22f;  // Width of the knob handle
    public float KnobHeight { get; set; } = 22f; // Height of the knob handle
    public float KnobBorderThickness { get; set; } = 2f;

    /// <summary>Optional formatting string or callback (e.g. "{0:0.0}" or "{0:P0}").</summary>
    public string FormatString { get; set; } = "{0:0.##}";
    public Func<float, string>? CustomFormatter { get; set; }

    /// <summary>Optional attached Label element to display the formatted value.</summary>
    public Label? ValueLabel { get; set; }

    public event Action<float>? OnValueChanged;

    // ── Construction ───────────────────────────────────────────────────────────

    public Slider()
    {
        Name = nameof(Slider);
        Color = Color.Transparent; // Track is rendered custom; background element is transparent
        Size = new UDim2(1f, 0f, 0f, 32f); // Default responsive size
    }

    public override void HandleInput(Vector2D mousePos, bool mousePressed, bool mouseReleased)
    {
        bool contains = AbsoluteBounds.Contains(mousePos);

        if (contains && !_isHovered)
        {
            _isHovered = true;
            if (!_isDragging) AnimateKnob(1.12f);
        }
        else if (!contains && _isHovered)
        {
            _isHovered = false;
            if (!_isDragging) AnimateKnob(1.0f);
        }

        if (contains && mousePressed)
        {
            _isDragging = true;
            AnimateKnob(1.28f);
            UpdateValueFromMouse(mousePos.X);
        }

        if (_isDragging)
        {
            UpdateValueFromMouse(mousePos.X);
            if (mouseReleased)
            {
                _isDragging = false;
                AnimateKnob(_isHovered ? 1.12f : 1.0f);
            }
        }

        base.HandleInput(mousePos, mousePressed, mouseReleased);
    }

    private void UpdateValueFromMouse(float mouseX)
    {
        var bounds = AbsoluteBounds;
        if (bounds.Width <= 0f) return;

        float padding = KnobWidth * 0.5f;
        float usableWidth = bounds.Width - padding * 2f;

        if (usableWidth <= 0f)
        {
            NormalizedValue = (mouseX - bounds.X) / bounds.Width;
        }
        else
        {
            float relativeX = mouseX - (bounds.X + padding);
            NormalizedValue = relativeX / usableWidth;
        }
    }

    private void UpdateValueLabel()
    {
        if (ValueLabel == null) return;

        if (CustomFormatter != null)
        {
            ValueLabel.Text = CustomFormatter(_value);
        }
        else if (!string.IsNullOrEmpty(FormatString))
        {
            ValueLabel.Text = string.Format(FormatString, _value);
        }
        else
        {
            ValueLabel.Text = _value.ToString("0.##");
        }
    }

    public override void Render(RenderContext context)
    {
        if (!Visible) return;

        var bounds = AbsoluteBounds;
        if (bounds.Width <= 0f || bounds.Height <= 0f) return;

        float centerY = bounds.Y + bounds.Height * 0.5f;
        float actualTrackHeight = MathF.Min(TrackHeight, bounds.Height);
        float trackY = centerY - actualTrackHeight * 0.5f;
        float trackRadius = actualTrackHeight * 0.5f;

        // 1. Draw Background Track Bar
        var trackBounds = new Rect(bounds.X, trackY, bounds.Width, actualTrackHeight);
        context.DrawQuad(trackBounds, TrackColor, trackRadius, ZIndex);

        // 2. Draw Progress Fill Bar
        float norm = NormalizedValue;
        float knobPadding = KnobWidth * 0.5f;
        float usableWidth = bounds.Width - knobPadding * 2f;
        float knobCenterX = (usableWidth > 0f)
            ? bounds.X + knobPadding + norm * usableWidth
            : bounds.X + norm * bounds.Width;

        float fillWidth = MathF.Max(0f, knobCenterX - bounds.X);
        if (fillWidth > 0f)
        {
            var fillBounds = new Rect(bounds.X, trackY, fillWidth, actualTrackHeight);
            context.DrawQuad(fillBounds, FillColor, trackRadius, ZIndex + 0.1f);
        }

        // 3. Draw Knob Handle (Scaled on hover / grab)
        float scaledKW = KnobWidth * _knobScale;
        float scaledKH = KnobHeight * _knobScale;
        var knobBounds = new Rect(
            knobCenterX - scaledKW * 0.5f,
            centerY - scaledKH * 0.5f,
            scaledKW,
            scaledKH
        );
        float knobRadius = MathF.Min(scaledKW, scaledKH) * 0.5f;

        Color activeKnobColor = _isHovered ? KnobHoverColor : KnobColor;

        // Draw Knob Drop Shadow
        var shadowBounds = new Rect(knobBounds.X + 1f, knobBounds.Y + 3f, knobBounds.Width, knobBounds.Height);
        context.DrawQuad(shadowBounds, Color.FromRgba(0, 0, 0, 80), knobRadius, ZIndex + 0.15f);

        // Draw Knob Base Quad
        context.DrawQuad(knobBounds, activeKnobColor, knobRadius, ZIndex + 0.2f);

        // Draw Knob Border Outline
        if (KnobBorderThickness > 0f)
        {
            context.DrawStroke(knobBounds, new StrokeInfo(KnobBorderThickness, KnobBorderColor), knobRadius, ZIndex + 0.3f);
        }

        // Render any child elements
        base.Render(context);
    }

    private void AnimateKnob(float targetScale)
    {
        _knobTween?.Stop();
        _knobTween = new OpenRei.Tween.Tween(_knobScale, targetScale, 0.15f, v => _knobScale = v, Easing.Cubic, EasingDirection.Out);
        _knobTween.Start();
    }
}
