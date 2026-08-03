using OpenRei.Graphics;
using OpenRei.Tween;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// A draggable slider bar (osu!-style) with a rounded track, a filled progress
/// portion, and a pill-shaped knob. Value is normalized 0..1. Drag to scrub;
/// optional value label mirrors osu!'s tooltip-style value display.
/// </summary>
public class Slider : Element
{
    private float _value;

    /// <summary>Current value, clamped to 0..1. Assigning fires <see cref="OnValueChanged"/>.</summary>
    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(clamped - _value) < 0.0001f) return;
            _value = clamped;
            OnValueChanged?.Invoke(_value);
            if (ValueLabel != null)
                ValueLabel.Text = _value.ToString("0.##");
        }
    }

    /// <summary>Fired whenever the value changes (including during drag scrubbing).</summary>
    public event Action<float>? OnValueChanged;

    // ── Track ──────────────────────────────────────────────────────────────
    public Color TrackColor { get; set; } = Color.FromRgba(40, 40, 48, 255);
    public Color FillColor { get; set; } = Color.FromRgba(120, 160, 255, 255);
    public float TrackCornerRadius { get; set; } = 7.5f;

    // ── Knob (pill) ────────────────────────────────────────────────────────
    public Color KnobColor { get; set; } = Color.White;
    public Color KnobBorderColor { get; set; } = Color.White;
    public float KnobBorderThickness { get; set; } = 3f;
    public float KnobWidth { get; set; } = 50f;
    public float KnobHeight { get; set; } = 15f;

    /// <summary>Optional label whose Text is updated with the current value (osu! tooltip-style).</summary>
    public Label? ValueLabel { get; set; }

    // ── Post-processing (default disabled) ──────────────────────────────────
    /// <summary>TODO: enable a glow effect under the knob (drop-shadow post-processing). Disabled until implemented.</summary>
    public bool KnobGlowEnabled { get; set; } = false;

    /// <summary>TODO: glow radius in pixels, used when <see cref="KnobGlowEnabled"/> is true.</summary>
    public float KnobGlowRadius { get; set; } = 8f;

    private bool _dragging;
    private float _knobScale = 1f;

    public Slider()
    {
        Name = nameof(Slider);
    }

    public override void HandleInput(Vector2D mousePos, bool mousePressed, bool mouseReleased)
    {
        bool contains = AbsoluteBounds.Contains(mousePos);

        if (mousePressed && contains)
        {
            _dragging = true;
            AnimateKnob(1.25f, Easing.Elastic);
        }

        if (mouseReleased)
        {
            _dragging = false;
            AnimateKnob(1f, Easing.Quintic);
        }

        if (_dragging)
        {
            float t = (mousePos.X - AbsoluteBounds.X) / AbsoluteBounds.Width;
            Value = t;
        }

        base.HandleInput(mousePos, mousePressed, mouseReleased);
    }

    public override void Render(RenderContext context)
    {
        if (!Visible) return;

        base.Render(context);

        var bounds = AbsoluteBounds;
        float centerY = bounds.Y + bounds.Height * 0.5f;
        float knobCenterX = bounds.X + Value * bounds.Width;

        // Track
        context.DrawQuad(bounds, TrackColor, TrackCornerRadius, ZIndex);

        // Fill (from left edge up to the knob position)
        float fillWidth = MathF.Max(0f, knobCenterX - bounds.X);
        if (fillWidth > 0f)
        {
            var fillBounds = new Rect(bounds.X, bounds.Y, fillWidth, bounds.Height);
            context.DrawQuad(fillBounds, FillColor, TrackCornerRadius, ZIndex + 0.1f);
        }

        // Knob (pill), scaled around its center while grabbed
        float knobW = KnobWidth * _knobScale;
        float knobH = KnobHeight * _knobScale;
        var knobBounds = new Rect(knobCenterX - knobW * 0.5f, centerY - knobH * 0.5f, knobW, knobH);
        float pillRadius = knobH * 0.5f;

        // TODO: if (KnobGlowEnabled) — add a DropShadowFilter glow under the knob here.

        context.DrawQuad(knobBounds, KnobColor, pillRadius, ZIndex + 0.2f);
        if (KnobBorderThickness > 0f)
        {
            context.DrawStroke(knobBounds, new StrokeInfo(KnobBorderThickness, KnobBorderColor), pillRadius, ZIndex + 0.3f);
        }
    }

    private void AnimateKnob(float target, Easing easing)
    {
        new OpenRei.Tween.Tween(_knobScale, target, 0.25f, v => _knobScale = v, easing, EasingDirection.Out).Start();
    }
}
