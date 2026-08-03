using OpenRei.Graphics;
using OpenRei.Tween;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// A 1:1 osu!-style interactive settings slider featuring a tall rounded track container with an inner-fitting
/// rounded rectangle knob handle, accent progress fills, hover/grab micro-animations, and step precision snapping.
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
    public Color KnobBorderColor { get; set; } = Color.FromRgba(255, 255, 255, 180);

    public float TrackCornerRadius { get; set; } = 8f;
    public float InnerPadding { get; set; } = 4f; // Space between outer track and inner knob/fill

    /// <summary>Width of the rounded rectangle knob handle.</summary>
    public float KnobWidth { get; set; } = 14f;

    /// <summary>Height of the knob handle. If 0, automatically fits inside the track height minus padding.</summary>
    public float KnobHeight { get; set; } = 0f;

    /// <summary>Corner radius of the rounded rectangle knob handle.</summary>
    public float KnobCornerRadius { get; set; } = 5f;

    public float KnobBorderThickness { get; set; } = 0f;

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
        Color = Color.Transparent; // Outer background is transparent; track is rendered custom
        Size = new UDim2(1f, 0f, 0f, 32f); // Default responsive slider height (32px tall bar)
    }

    public override void HandleInput(Vector2D mousePos, bool mousePressed, bool mouseReleased)
    {
        bool contains = AbsoluteBounds.Contains(mousePos);

        if (contains && !_isHovered)
        {
            _isHovered = true;
            if (!_isDragging) AnimateKnob(1.15f);
        }
        else if (!contains && _isHovered)
        {
            _isHovered = false;
            if (!_isDragging) AnimateKnob(1.0f);
        }

        if (contains && mousePressed)
        {
            _isDragging = true;
            AnimateKnob(1.25f);
            UpdateValueFromMouse(mousePos.X);
        }

        if (_isDragging)
        {
            UpdateValueFromMouse(mousePos.X);
            if (mouseReleased)
            {
                _isDragging = false;
                AnimateKnob(_isHovered ? 1.15f : 1.0f);
            }
        }

        base.HandleInput(mousePos, mousePressed, mouseReleased);
    }

    private void UpdateValueFromMouse(float mouseX)
    {
        var bounds = AbsoluteBounds;
        if (bounds.Width <= 0f) return;

        float paddingLeft = InnerPadding + KnobWidth * 0.5f;
        float paddingRight = InnerPadding + KnobWidth * 0.5f;
        float usableWidth = bounds.Width - (paddingLeft + paddingRight);

        if (usableWidth <= 0f)
        {
            NormalizedValue = (mouseX - bounds.X) / bounds.Width;
        }
        else
        {
            float relativeX = mouseX - (bounds.X + paddingLeft);
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

        // 1. Draw Tall Outer Track Container Bar
        context.DrawQuad(bounds, TrackColor, TrackCornerRadius, ZIndex);

        // Calculate inner track bounds with InnerPadding
        float pad = InnerPadding;
        float innerX = bounds.X + pad;
        float innerY = bounds.Y + pad;
        float innerW = MathF.Max(0f, bounds.Width - pad * 2f);
        float innerH = MathF.Max(0f, bounds.Height - pad * 2f);

        // Knob dimensions (fits inside inner height)
        float kw = KnobWidth * _knobScale;
        float kh = (KnobHeight > 0f) ? MathF.Min(KnobHeight, innerH) : innerH;
        float knobY = innerY + (innerH - kh) * 0.5f;

        float norm = NormalizedValue;
        float knobPaddingX = kw * 0.5f;
        float usableWidth = MathF.Max(0f, innerW - kw);
        float knobCenterX = innerX + knobPaddingX + norm * usableWidth;

        // 2. Draw Progress Fill Bar (left edge to knob center)
        float fillWidth = MathF.Max(0f, knobCenterX - bounds.X);
        if (fillWidth > 0f)
        {
            float fillRadius = MathF.Max(0f, TrackCornerRadius - pad);
            var fillBounds = new Rect(bounds.X, bounds.Y, fillWidth, bounds.Height);
            context.DrawQuad(fillBounds, FillColor, fillRadius, ZIndex + 0.1f);
        }

        // 3. Draw Rounded Rect Knob Handle (Fits inside track bar)
        var knobBounds = new Rect(
            knobCenterX - kw * 0.5f,
            knobY,
            kw,
            kh
        );

        Color activeKnobColor = _isHovered ? KnobHoverColor : KnobColor;

        // Draw Knob Base Quad (Rounded Rectangle)
        context.DrawQuad(knobBounds, activeKnobColor, KnobCornerRadius, ZIndex + 0.2f);

        // Draw Knob Border Outline (if thickness > 0)
        if (KnobBorderThickness > 0f)
        {
            context.DrawStroke(knobBounds, new StrokeInfo(KnobBorderThickness, KnobBorderColor), KnobCornerRadius, ZIndex + 0.3f);
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
