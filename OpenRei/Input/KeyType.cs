namespace OpenRei.InputSystem;

/// <summary>
/// Unified key types for Keyboard keys, Mouse buttons, and Gamepad controls.
/// </summary>
public enum KeyType
{
    Unknown,

    // Mouse Buttons
    MouseLeft,
    MouseRight,
    MouseMiddle,

    // Keyboard Common
    Space,
    Enter,
    Escape,
    Tab,
    Backspace,
    Up,
    Down,
    Left,
    Right,

    // Alphanumeric
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    Num0, Num1, Num2, Num3, Num4, Num5, Num6, Num7, Num8, Num9,

    // Function Keys
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    // Modifiers & System
    LeftShift,
    RightShift,
    LeftControl,
    RightControl,
    LeftAlt,
    RightAlt,
    LeftSuper,  // Windows / Command key Left
    RightSuper, // Windows / Command key Right
    CapsLock,
    NumLock,
    ScrollLock,

    // Navigation & Editing
    Insert,
    Delete,
    Home,
    End,
    PageUp,
    PageDown,
    PrintScreen,
    Pause,

    // Gamepad
    GamepadA,
    GamepadB,
    GamepadX,
    GamepadY,
    GamepadDpadUp,
    GamepadDpadDown,
    GamepadDpadLeft,
    GamepadDpadRight
}
