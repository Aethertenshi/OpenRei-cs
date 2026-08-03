using OpenRei.Graphics;
using OpenRei.Tween;
using OpenRei.Types;

namespace OpenRei.Elements;

/// <summary>
/// A 1:1 osu!-style interactive settings slider featuring a rounded track bar, filled progress area,
/// and a highlighted right-end knob cap handle matching the osu! settings page design.
/// </summary>
public class Slider : Element
{
    private float _value = 0.5f;
    private float _min = 0f;
    private float _max = 1f;
    private float _step = 0f; // 0 = continuous

    private bool _isDragging;
    private bool _isHovered;

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

    public Color TrackColor { get; set; } = Color.FromRgba(26, 25, 33, 255);      // Dark track
    public Color FillColor { get; set; } = Color.FromRgba(104, 81, 214, 255);    // osu! Purple Fill
    public Color KnobColor { get; set; } = Color.FromRgba(140, 116, 248, 255);   // Lighter Lavender End Cap
    public Color KnobHoverColor { get; set; } = Color.FromRgba(163, 142, 250, 255);

    /// <summary>Width of the right-end knob cap handle (in pixels).</summary>
    public float KnobWidth { get; set; } = 10f;

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
        Color = Color.Transparent; // Base background transparent; custom track rendered below
        CornerRadius = 8f;         // Default rounded corners matching osu! settings
        Size = new UDim2(1f, 0f, 0f, 32f); // Default responsive height
    }

    public override void HandleInput(Vector2D mousePos, bool mousePressed, bool mouseReleased)
    {
        bool contains = AbsoluteBounds.Contains(mousePos);

        if (contains && !_isHovered)
        {
            _isHovered = true;
        }
        else if (!contains && _isHovered)
        {
            _isHovered = false;
        }

        if (contains && mousePressed)
        {
            _isDragging = true;
            UpdateValueFromMouse(mousePos.X);
        }

        if (_isDragging)
        {
            UpdateValueFromMouse(mousePos.X);
            if (mouseReleased)
            {
                _isDragging = false;
            }
        }

        base.HandleInput(mousePos, mousePressed, mouseReleased);
    }

    private void UpdateValueFromMouse(float mouseX)
    {
        var bounds = AbsoluteBounds;
        if (bounds.Width <= 0f) return;

        float relativeX = mouseX - bounds.X;
        NormalizedValue = relativeX / bounds.Width;
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

        float norm = NormalizedValue;

        // 1. Outer Dark Rounded Track Container
        context.DrawQuad(bounds, TrackColor, CornerRadius, ZIndex);

        // 2. Filled Progress Area (Left edge up to Value * Width)
        float fillWidth = norm * bounds.Width;
        if (fillWidth > 0f)
        {
            var fillBounds = new Rect(bounds.X, bounds.Y, fillWidth, bounds.Height);
            context.DrawQuad(fillBounds, FillColor, CornerRadius, ZIndex + 0.1f);

            // 3. Knob Handle Cap (Lighter Accent Bar at the right edge of the fill)
            if (KnobWidth > 0f)
            {
                float kw = MathF.Min(KnobWidth, fillWidth);
                var knobBounds = new Rect(bounds.X + fillWidth - kw, bounds.Y, kw, bounds.Height);
                Color activeKnobColor = _isHovered ? KnobHoverColor : KnobColor;
                context.DrawQuad(knobBounds, activeKnobColor, CornerRadius, ZIndex + 0.2f);
            }
        }

        // Render any child elements
        base.Render(context);
    }
}
