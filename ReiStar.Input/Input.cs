namespace reistar.Input;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SDL;
using reistar.Maths;

public static unsafe class Input
{
    private static readonly HashSet<Keys> _heldKeys = new();
    private static readonly HashSet<Keys> _pressedThisFrame = new();
    private static readonly HashSet<Keys> _releasedThisFrame = new();

    private static readonly HashSet<MouseButton> _heldMouseButtons = new();
    private static readonly HashSet<MouseButton> _mousePressedThisFrame = new();
    private static readonly HashSet<MouseButton> _mouseReleasedThisFrame = new();

    private static readonly Dictionary<Keys, List<Action>> _keyClickedBinds = new();
    private static readonly Dictionary<Keys, List<Action<float>>> _keyPressedBinds = new();
    private static readonly Dictionary<Keys, List<Action>> _keyReleasedBinds = new();

    private static readonly Dictionary<MouseButton, List<Action<Vect2D>>> _mouseDownBinds = new();
    private static readonly Dictionary<MouseButton, List<Action<Vect2D>>> _mouseUpBinds = new();

    public static Vect2D MousePosition { get; private set; } = Vect2D.Zero;
    public static Vect2D MouseDelta { get; private set; } = Vect2D.Zero;
    public static float MouseWheel { get; private set; } = 0f;

    /// <summary>
    /// When set to true (e.g. while a text input box is focused), global shortcut binds will be suppressed.
    /// </summary>
    public static bool BlockGlobalKeys { get; set; } = false;

    /// <summary>
    /// Triggered whenever an external file is dragged and dropped into the game window.
    /// </summary>
    public static event Action<string>? OnFileDropped;

    /// <summary>
    /// Binds an action to fire once when the specified key is pressed down.
    /// </summary>
    public static void KeyClicked(Keys key, Action action)
    {
        if (!_keyClickedBinds.TryGetValue(key, out var list))
        {
            list = new List<Action>();
            _keyClickedBinds[key] = list;
        }
        list.Add(action);
    }

    /// <summary>
    /// Binds an action to fire continuously every frame with deltaTime while the specified key is held down.
    /// </summary>
    public static void KeyPressed(Keys key, Action<float> action)
    {
        if (!_keyPressedBinds.TryGetValue(key, out var list))
        {
            list = new List<Action<float>>();
            _keyPressedBinds[key] = list;
        }
        list.Add(action);
    }

    /// <summary>
    /// Binds an action to fire once when the specified key is released.
    /// </summary>
    public static void KeyReleased(Keys key, Action action)
    {
        if (!_keyReleasedBinds.TryGetValue(key, out var list))
        {
            list = new List<Action>();
            _keyReleasedBinds[key] = list;
        }
        list.Add(action);
    }

    public static void OnMouseDown(MouseButton button, Action<Vect2D> action)
    {
        if (!_mouseDownBinds.TryGetValue(button, out var list))
        {
            list = new List<Action<Vect2D>>();
            _mouseDownBinds[button] = list;
        }
        list.Add(action);
    }

    public static void OnMouseUp(MouseButton button, Action<Vect2D> action)
    {
        if (!_mouseUpBinds.TryGetValue(button, out var list))
        {
            list = new List<Action<Vect2D>>();
            _mouseUpBinds[button] = list;
        }
        list.Add(action);
    }

    public static bool IsKeyDown(Keys key) => _heldKeys.Contains(key);
    public static bool IsKeyPressed(Keys key) => _pressedThisFrame.Contains(key);
    public static bool IsKeyClicked(Keys key) => _pressedThisFrame.Contains(key);
    public static bool IsKeyReleased(Keys key) => _releasedThisFrame.Contains(key);

    public static bool IsMouseButtonDown(MouseButton button) => _heldMouseButtons.Contains(button);
    public static bool IsMouseButtonClicked(MouseButton button) => _mousePressedThisFrame.Contains(button);
    public static bool IsMouseButtonReleased(MouseButton button) => _mouseReleasedThisFrame.Contains(button);

    /// <summary>
    /// Ingests a native SDL3 event and updates input tables accordingly.
    /// </summary>
    public static void ProcessEvent(in SDL_Event ev)
    {
        uint type = ev.type;

        if (type == (uint)SDL_EventType.SDL_EVENT_KEY_DOWN)
        {
            Keys key = MapSdlKeycode((SDL_Keycode)ev.key.key);
            if (key != Keys.None)
            {
                if (!_heldKeys.Contains(key))
                {
                    _pressedThisFrame.Add(key);
                    _heldKeys.Add(key);

                    if (!BlockGlobalKeys && _keyClickedBinds.TryGetValue(key, out var actions))
                    {
                        for (int i = 0; i < actions.Count; i++)
                        {
                            actions[i]();
                        }
                    }
                }
            }
        }
        else if (type == (uint)SDL_EventType.SDL_EVENT_KEY_UP)
        {
            Keys key = MapSdlKeycode((SDL_Keycode)ev.key.key);
            if (key != Keys.None)
            {
                _heldKeys.Remove(key);
                _releasedThisFrame.Add(key);

                if (!BlockGlobalKeys && _keyReleasedBinds.TryGetValue(key, out var actions))
                {
                    for (int i = 0; i < actions.Count; i++)
                    {
                        actions[i]();
                    }
                }
            }
        }
        else if (type == (uint)SDL_EventType.SDL_EVENT_MOUSE_MOTION)
        {
            MousePosition = new Vect2D(ev.motion.x, ev.motion.y);
            MouseDelta = new Vect2D(ev.motion.xrel, ev.motion.yrel);
        }
        else if (type == (uint)SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN)
        {
            MouseButton btn = MapSdlMouseButton(ev.button.button);
            if (btn != MouseButton.None)
            {
                _heldMouseButtons.Add(btn);
                _mousePressedThisFrame.Add(btn);
                MousePosition = new Vect2D(ev.button.x, ev.button.y);

                if (_mouseDownBinds.TryGetValue(btn, out var actions))
                {
                    for (int i = 0; i < actions.Count; i++)
                    {
                        actions[i](MousePosition);
                    }
                }
            }
        }
        else if (type == (uint)SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP)
        {
            MouseButton btn = MapSdlMouseButton(ev.button.button);
            if (btn != MouseButton.None)
            {
                _heldMouseButtons.Remove(btn);
                _mouseReleasedThisFrame.Add(btn);
                MousePosition = new Vect2D(ev.button.x, ev.button.y);

                if (_mouseUpBinds.TryGetValue(btn, out var actions))
                {
                    for (int i = 0; i < actions.Count; i++)
                    {
                        actions[i](MousePosition);
                    }
                }
            }
        }
        else if (type == (uint)SDL_EventType.SDL_EVENT_MOUSE_WHEEL)
        {
            MouseWheel = ev.wheel.y;
        }
        else if (type == (uint)SDL_EventType.SDL_EVENT_DROP_FILE)
        {
            byte* dropData = (byte*)ev.drop.data;
            if (dropData != null)
            {
                string? filePath = Marshal.PtrToStringUTF8((IntPtr)dropData);
                if (!string.IsNullOrEmpty(filePath))
                {
                    OnFileDropped?.Invoke(filePath);
                }
            }
        }
    }

    /// <summary>
    /// Dispatches held-key actions and clears single-frame transient states.
    /// Called once per frame by the engine loop.
    /// </summary>
    public static void Update(float deltaTime)
    {
        if (!BlockGlobalKeys && _keyPressedBinds.Count > 0)
        {
            foreach (var kvp in _keyPressedBinds)
            {
                if (_heldKeys.Contains(kvp.Key))
                {
                    var actions = kvp.Value;
                    for (int i = 0; i < actions.Count; i++)
                    {
                        actions[i](deltaTime);
                    }
                }
            }
        }

        _pressedThisFrame.Clear();
        _releasedThisFrame.Clear();
        _mousePressedThisFrame.Clear();
        _mouseReleasedThisFrame.Clear();
        MouseDelta = Vect2D.Zero;
        MouseWheel = 0f;
    }

    private static MouseButton MapSdlMouseButton(byte button) => button switch
    {
        1 => MouseButton.Left,
        2 => MouseButton.Middle,
        3 => MouseButton.Right,
        4 => MouseButton.X1,
        5 => MouseButton.X2,
        _ => MouseButton.None
    };

    private static Keys MapSdlKeycode(SDL_Keycode key) => key switch
    {
        SDL_Keycode.SDLK_A => Keys.A,
        SDL_Keycode.SDLK_B => Keys.B,
        SDL_Keycode.SDLK_C => Keys.C,
        SDL_Keycode.SDLK_D => Keys.D,
        SDL_Keycode.SDLK_E => Keys.E,
        SDL_Keycode.SDLK_F => Keys.F,
        SDL_Keycode.SDLK_G => Keys.G,
        SDL_Keycode.SDLK_H => Keys.H,
        SDL_Keycode.SDLK_I => Keys.I,
        SDL_Keycode.SDLK_J => Keys.J,
        SDL_Keycode.SDLK_K => Keys.K,
        SDL_Keycode.SDLK_L => Keys.L,
        SDL_Keycode.SDLK_M => Keys.M,
        SDL_Keycode.SDLK_N => Keys.N,
        SDL_Keycode.SDLK_O => Keys.O,
        SDL_Keycode.SDLK_P => Keys.P,
        SDL_Keycode.SDLK_Q => Keys.Q,
        SDL_Keycode.SDLK_R => Keys.R,
        SDL_Keycode.SDLK_S => Keys.S,
        SDL_Keycode.SDLK_T => Keys.T,
        SDL_Keycode.SDLK_U => Keys.U,
        SDL_Keycode.SDLK_V => Keys.V,
        SDL_Keycode.SDLK_W => Keys.W,
        SDL_Keycode.SDLK_X => Keys.X,
        SDL_Keycode.SDLK_Y => Keys.Y,
        SDL_Keycode.SDLK_Z => Keys.Z,

        SDL_Keycode.SDLK_0 => Keys.D0,
        SDL_Keycode.SDLK_1 => Keys.D1,
        SDL_Keycode.SDLK_2 => Keys.D2,
        SDL_Keycode.SDLK_3 => Keys.D3,
        SDL_Keycode.SDLK_4 => Keys.D4,
        SDL_Keycode.SDLK_5 => Keys.D5,
        SDL_Keycode.SDLK_6 => Keys.D6,
        SDL_Keycode.SDLK_7 => Keys.D7,
        SDL_Keycode.SDLK_8 => Keys.D8,
        SDL_Keycode.SDLK_9 => Keys.D9,

        SDL_Keycode.SDLK_KP_0 => Keys.NumPad0,
        SDL_Keycode.SDLK_KP_1 => Keys.NumPad1,
        SDL_Keycode.SDLK_KP_2 => Keys.NumPad2,
        SDL_Keycode.SDLK_KP_3 => Keys.NumPad3,
        SDL_Keycode.SDLK_KP_4 => Keys.NumPad4,
        SDL_Keycode.SDLK_KP_5 => Keys.NumPad5,
        SDL_Keycode.SDLK_KP_6 => Keys.NumPad6,
        SDL_Keycode.SDLK_KP_7 => Keys.NumPad7,
        SDL_Keycode.SDLK_KP_8 => Keys.NumPad8,
        SDL_Keycode.SDLK_KP_9 => Keys.NumPad9,
        SDL_Keycode.SDLK_KP_DECIMAL => Keys.NumPadDecimal,
        SDL_Keycode.SDLK_KP_DIVIDE => Keys.NumPadDivide,
        SDL_Keycode.SDLK_KP_MULTIPLY => Keys.NumPadMultiply,
        SDL_Keycode.SDLK_KP_MINUS => Keys.NumPadSubtract,
        SDL_Keycode.SDLK_KP_PLUS => Keys.NumPadAdd,
        SDL_Keycode.SDLK_KP_ENTER => Keys.NumPadEnter,

        SDL_Keycode.SDLK_F1 => Keys.F1,
        SDL_Keycode.SDLK_F2 => Keys.F2,
        SDL_Keycode.SDLK_F3 => Keys.F3,
        SDL_Keycode.SDLK_F4 => Keys.F4,
        SDL_Keycode.SDLK_F5 => Keys.F5,
        SDL_Keycode.SDLK_F6 => Keys.F6,
        SDL_Keycode.SDLK_F7 => Keys.F7,
        SDL_Keycode.SDLK_F8 => Keys.F8,
        SDL_Keycode.SDLK_F9 => Keys.F9,
        SDL_Keycode.SDLK_F10 => Keys.F10,
        SDL_Keycode.SDLK_F11 => Keys.F11,
        SDL_Keycode.SDLK_F12 => Keys.F12,

        SDL_Keycode.SDLK_ESCAPE => Keys.Escape,
        SDL_Keycode.SDLK_RETURN => Keys.Enter,
        SDL_Keycode.SDLK_SPACE => Keys.Space,
        SDL_Keycode.SDLK_TAB => Keys.Tab,
        SDL_Keycode.SDLK_BACKSPACE => Keys.Backspace,
        SDL_Keycode.SDLK_INSERT => Keys.Insert,
        SDL_Keycode.SDLK_DELETE => Keys.Delete,
        SDL_Keycode.SDLK_HOME => Keys.Home,
        SDL_Keycode.SDLK_END => Keys.End,
        SDL_Keycode.SDLK_PAGEUP => Keys.PageUp,
        SDL_Keycode.SDLK_PAGEDOWN => Keys.PageDown,

        SDL_Keycode.SDLK_LEFT => Keys.Left,
        SDL_Keycode.SDLK_RIGHT => Keys.Right,
        SDL_Keycode.SDLK_UP => Keys.Up,
        SDL_Keycode.SDLK_DOWN => Keys.Down,

        SDL_Keycode.SDLK_LSHIFT => Keys.LeftShift,
        SDL_Keycode.SDLK_RSHIFT => Keys.RightShift,
        SDL_Keycode.SDLK_LCTRL => Keys.LeftCtrl,
        SDL_Keycode.SDLK_RCTRL => Keys.RightCtrl,
        SDL_Keycode.SDLK_LALT => Keys.LeftAlt,
        SDL_Keycode.SDLK_RALT => Keys.RightAlt,
        SDL_Keycode.SDLK_LGUI => Keys.LeftGui,
        SDL_Keycode.SDLK_RGUI => Keys.RightGui,

        SDL_Keycode.SDLK_CAPSLOCK => Keys.CapsLock,
        SDL_Keycode.SDLK_SCROLLLOCK => Keys.ScrollLock,
        SDL_Keycode.SDLK_NUMLOCKCLEAR => Keys.NumLock,
        SDL_Keycode.SDLK_PRINTSCREEN => Keys.PrintScreen,
        SDL_Keycode.SDLK_PAUSE => Keys.Pause,

        SDL_Keycode.SDLK_GRAVE => Keys.Grave,
        SDL_Keycode.SDLK_MINUS => Keys.Minus,
        SDL_Keycode.SDLK_EQUALS => Keys.Equals,
        SDL_Keycode.SDLK_LEFTBRACKET => Keys.LeftBracket,
        SDL_Keycode.SDLK_RIGHTBRACKET => Keys.RightBracket,
        SDL_Keycode.SDLK_BACKSLASH => Keys.Backslash,
        SDL_Keycode.SDLK_SEMICOLON => Keys.Semicolon,
        SDL_Keycode.SDLK_APOSTROPHE => Keys.Apostrophe,
        SDL_Keycode.SDLK_COMMA => Keys.Comma,
        SDL_Keycode.SDLK_PERIOD => Keys.Period,
        SDL_Keycode.SDLK_SLASH => Keys.Slash,

        _ => Keys.None
    };
}
