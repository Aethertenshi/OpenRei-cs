namespace OpenRei.InputSystem;

/// <summary>
/// Unified key types for Keyboard keys, Mouse buttons, and Gamepad controls.
/// </summary>
public enum KeyType
{
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

    // Modifiers
    LeftShift,
    RightShift,
    LeftControl,
    RightControl,
    LeftAlt,
    RightAlt,

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
