namespace reistar.Points.UI;

using System;
using System.Collections.Generic;
using reistar.Maths;
using reistar.Graphics;
using reistar.Shapes;
using reistar.Input;

public class TextBox : UIElement
{
    private string _text = string.Empty;
    private Font? _font;
    private float _fontSize = 14f;
    private Color _textColor = Color.White;
    private Color _placeholderColor = new Color(130, 130, 140, 180);
    private Color _focusedBorderColor = Color.White;
    private Color _cursorColor = Color.White;
    private float _cursorWidth = 2f;
    private int _cursorIndex = 0;
    private float _cursorBlinkTimer = 0f;
    private bool _cursorVisible = true;

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value ?? string.Empty;
                _cursorIndex = Math.Clamp(_cursorIndex, 0, _text.Length);
                OnChange?.Invoke(_text);
                MarkDirty();
            }
        }
    }

    public string CurrentValue
    {
        get => Text;
        set => Text = value;
    }

    public string Placeholder { get; set; } = string.Empty;

    public Color PlaceholderColor
    {
        get => _placeholderColor;
        set => _placeholderColor = value;
    }

    public Font? Font
    {
        get => _font;
        set
        {
            if (_font != value)
            {
                _font = value;
                MarkDirty();
            }
        }
    }

    public float FontSize
    {
        get => _fontSize;
        set
        {
            if (_fontSize != value)
            {
                _fontSize = value;
                MarkDirty();
            }
        }
    }

    public Color TextColor
    {
        get => _textColor;
        set => _textColor = value;
    }

    public Color FocusedBorderColor
    {
        get => _focusedBorderColor;
        set => _focusedBorderColor = value;
    }

    public Color CursorColor
    {
        get => _cursorColor;
        set => _cursorColor = value;
    }

    public float CursorWidth
    {
        get => _cursorWidth;
        set => _cursorWidth = value;
    }

    public bool Focused { get; private set; }

    public event Action<string>? OnChange;
    public event Action<string>? OnFocusLost;
    public event Action? OnFocusEnter;

    public TextBox()
    {
        InterceptsMouse = true;
        Size = UVect.FromOffset(240, 36);
        BackgroundColor = new Color(20, 20, 28, 220);
        BorderColor = new Color(50, 50, 65, 255);
        BorderThickness = 1f;
        Padding = 6f;
    }

    public void Focus()
    {
        if (Focused) return;
        Focused = true;
        Input.BlockGlobalKeys = true;
        _cursorBlinkTimer = 0f;
        _cursorVisible = true;
        OnFocusEnter?.Invoke();
    }

    public void Unfocus()
    {
        if (!Focused) return;
        Focused = false;
        Input.BlockGlobalKeys = false;
        OnFocusLost?.Invoke(_text);
    }

    public override bool ProcessMouseDown(Vect2D mousePos, MouseButton button)
    {
        bool wasHit = ContainsPoint(mousePos);
        if (wasHit)
        {
            Focus();
            _cursorIndex = _text.Length;
        }
        else if (Focused)
        {
            Unfocus();
        }

        return base.ProcessMouseDown(mousePos, button);
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (!Focused) return;

        // Cursor blink timer
        _cursorBlinkTimer += deltaTime;
        if (_cursorBlinkTimer >= 0.5f)
        {
            _cursorBlinkTimer = 0f;
            _cursorVisible = !_cursorVisible;
        }

        // Process typing input
        if (Input.IsKeyPressed(Keys.Backspace))
        {
            if (_cursorIndex > 0 && _text.Length > 0)
            {
                _text = _text.Remove(_cursorIndex - 1, 1);
                _cursorIndex--;
                OnChange?.Invoke(_text);
                _cursorBlinkTimer = 0f;
                _cursorVisible = true;
            }
        }
        else if (Input.IsKeyPressed(Keys.Delete))
        {
            if (_cursorIndex < _text.Length)
            {
                _text = _text.Remove(_cursorIndex, 1);
                OnChange?.Invoke(_text);
                _cursorBlinkTimer = 0f;
                _cursorVisible = true;
            }
        }
        else if (Input.IsKeyPressed(Keys.Left))
        {
            if (_cursorIndex > 0) _cursorIndex--;
            _cursorBlinkTimer = 0f;
            _cursorVisible = true;
        }
        else if (Input.IsKeyPressed(Keys.Right))
        {
            if (_cursorIndex < _text.Length) _cursorIndex++;
            _cursorBlinkTimer = 0f;
            _cursorVisible = true;
        }
        else if (Input.IsKeyPressed(Keys.Escape) || Input.IsKeyPressed(Keys.Enter))
        {
            Unfocus();
        }
        else
        {
            // Process alphanumeric and symbol keys
            char? typedChar = GetTypedCharacter();
            if (typedChar.HasValue)
            {
                _text = _text.Insert(_cursorIndex, typedChar.Value.ToString());
                _cursorIndex++;
                OnChange?.Invoke(_text);
                _cursorBlinkTimer = 0f;
                _cursorVisible = true;
            }
        }
    }

    private char? GetTypedCharacter()
    {
        bool shift = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);

        // A-Z
        for (int k = (int)Keys.A; k <= (int)Keys.Z; k++)
        {
            Keys key = (Keys)k;
            if (Input.IsKeyPressed(key))
            {
                char c = (char)('a' + (k - (int)Keys.A));
                return shift ? char.ToUpper(c) : c;
            }
        }

        // 0-9
        for (int k = (int)Keys.D0; k <= (int)Keys.D9; k++)
        {
            Keys key = (Keys)k;
            if (Input.IsKeyPressed(key))
            {
                int digit = k - (int)Keys.D0;
                if (!shift) return (char)('0' + digit);
                return digit switch
                {
                    1 => '!', 2 => '@', 3 => '#', 4 => '$', 5 => '%',
                    6 => '^', 7 => '&', 8 => '*', 9 => '(', 0 => ')',
                    _ => (char)('0' + digit)
                };
            }
        }

        if (Input.IsKeyPressed(Keys.Space)) return ' ';
        if (Input.IsKeyPressed(Keys.Minus)) return shift ? '_' : '-';
        if (Input.IsKeyPressed(Keys.Equals)) return shift ? '+' : '=';
        if (Input.IsKeyPressed(Keys.Comma)) return shift ? '<' : ',';
        if (Input.IsKeyPressed(Keys.Period)) return shift ? '>' : '.';
        if (Input.IsKeyPressed(Keys.Slash)) return shift ? '?' : '/';
        if (Input.IsKeyPressed(Keys.Semicolon)) return shift ? ':' : ';';
        if (Input.IsKeyPressed(Keys.Apostrophe)) return shift ? '"' : '\'';

        return null;
    }

    public override void Draw(IRenderer renderer)
    {
        if (!Visible || SkipDraw) return;

        int effectiveZIndex = (CalculatedDepth * 10) + ZIndex;

        if (BackgroundColor.A > 0)
        {
            Shapes.DrawRect(renderer, ResolvedTopLeft, ResolvedSize, BackgroundColor, Anchor.TopLeft, effectiveZIndex);
        }

        Color currentBorder = Focused ? FocusedBorderColor : BorderColor;
        if (currentBorder.A > 0 && BorderThickness > 0)
        {
            Shapes.DrawRectOutline(renderer, ResolvedTopLeft, ResolvedSize, BorderThickness, currentBorder, Anchor.TopLeft, effectiveZIndex + 1);
        }

        float textX = ResolvedTopLeft.X + Padding + 4f;
        float textY = ResolvedTopLeft.Y + (ResolvedSize.Y - _fontSize) * 0.5f;

        if (!string.IsNullOrEmpty(_text) && _font != null)
        {
            Shapes.DrawText(renderer, _font, _text, new Vect2D(textX, textY), _fontSize, _textColor, Anchor.TopLeft, effectiveZIndex + 2);
        }
        else if (string.IsNullOrEmpty(_text) && !string.IsNullOrEmpty(Placeholder) && _font != null)
        {
            Shapes.DrawText(renderer, _font, Placeholder, new Vect2D(textX, textY), _fontSize, _placeholderColor, Anchor.TopLeft, effectiveZIndex + 2);
        }

        // Draw animated cursor when focused
        if (Focused && _cursorVisible && _font != null)
        {
            string preCursorText = _text.Substring(0, Math.Clamp(_cursorIndex, 0, _text.Length));
            float cursorOffset = _font.MeasureString(preCursorText, _fontSize).X;
            float cursorX = textX + cursorOffset;
            float cursorHeight = _fontSize * 1.1f;
            float cursorY = ResolvedTopLeft.Y + (ResolvedSize.Y - cursorHeight) * 0.5f;

            Shapes.DrawRect(
                renderer,
                new Vect2D(cursorX, cursorY),
                new Vect2D(_cursorWidth, cursorHeight),
                _cursorColor,
                Anchor.TopLeft,
                effectiveZIndex + 3
            );
        }
    }
}
