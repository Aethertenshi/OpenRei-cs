namespace ReiStar.VisualEditor.Backend;

using System;
using System.Numerics;
using System.Text;
using Hexa.NET.ImGui;
using SDL;

/// <summary>
/// Managed SDL3 input translator for Dear ImGui using ppy.SDL3-CS and Hexa.NET.ImGui.
/// Pure C# without raw P/Invokes.
/// </summary>
public static unsafe class ImGuiSdl3Input
{
    private static ulong _lastTime = 0;

    public static void Initialize(nint windowHandle)
    {
        var io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;

        _lastTime = SDL3.SDL_GetPerformanceCounter();
    }

    public static void NewFrame(int windowWidth, int windowHeight, float displayScale = 1.0f)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(windowWidth, windowHeight);
        io.DisplayFramebufferScale = new Vector2(displayScale, displayScale);

        ulong currentTime = SDL3.SDL_GetPerformanceCounter();
        ulong frequency = SDL3.SDL_GetPerformanceFrequency();
        if (_lastTime > 0 && frequency > 0)
        {
            io.DeltaTime = MathF.Max((float)(currentTime - _lastTime) / frequency, 1.0f / 1000.0f);
        }
        else
        {
            io.DeltaTime = 1.0f / 60.0f;
        }
        _lastTime = currentTime;
    }

    public static bool ProcessEvent(in SDL_Event ev)
    {
        var io = ImGui.GetIO();

        switch ((SDL_EventType)ev.type)
        {
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                io.AddMousePosEvent(ev.motion.x, ev.motion.y);
                return io.WantCaptureMouse;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                {
                    bool down = ev.type == (uint)SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN;
                    int button = -1;
                    if (ev.button.button == SDL3.SDL_BUTTON_LEFT) button = 0;
                    else if (ev.button.button == SDL3.SDL_BUTTON_RIGHT) button = 1;
                    else if (ev.button.button == SDL3.SDL_BUTTON_MIDDLE) button = 2;
                    else if (ev.button.button == SDL3.SDL_BUTTON_X1) button = 3;
                    else if (ev.button.button == SDL3.SDL_BUTTON_X2) button = 4;

                    if (button >= 0)
                    {
                        io.AddMouseButtonEvent(button, down);
                    }
                    return io.WantCaptureMouse;
                }

            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                io.AddMouseWheelEvent(-ev.wheel.x, ev.wheel.y);
                return io.WantCaptureMouse;

            case SDL_EventType.SDL_EVENT_TEXT_INPUT:
                {
                    byte* textPtr = ev.text.text;
                    if (textPtr != null)
                    {
                        io.AddInputCharactersUTF8(textPtr);
                    }
                    return io.WantCaptureKeyboard;
                }

            case SDL_EventType.SDL_EVENT_KEY_DOWN:
            case SDL_EventType.SDL_EVENT_KEY_UP:
                {
                    bool down = ev.type == (uint)SDL_EventType.SDL_EVENT_KEY_DOWN;
                    UpdateKeyModifiers(ev.key.mod);
                    ImGuiKey key = SdlKeycodeToImGuiKey(ev.key.key);
                    if (key != ImGuiKey.None)
                    {
                        io.AddKeyEvent(key, down);
                    }
                    return io.WantCaptureKeyboard;
                }
        }

        return false;
    }

    private static void UpdateKeyModifiers(SDL_Keymod mod)
    {
        var io = ImGui.GetIO();
        io.AddKeyEvent(ImGuiKey.ModCtrl, (mod & SDL_Keymod.SDL_KMOD_CTRL) != 0);
        io.AddKeyEvent(ImGuiKey.ModShift, (mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0);
        io.AddKeyEvent(ImGuiKey.ModAlt, (mod & SDL_Keymod.SDL_KMOD_ALT) != 0);
        io.AddKeyEvent(ImGuiKey.ModSuper, (mod & SDL_Keymod.SDL_KMOD_GUI) != 0);
    }

    private static ImGuiKey SdlKeycodeToImGuiKey(SDL_Keycode keycode)
    {
        return keycode switch
        {
            SDL_Keycode.SDLK_TAB => ImGuiKey.Tab,
            SDL_Keycode.SDLK_LEFT => ImGuiKey.LeftArrow,
            SDL_Keycode.SDLK_RIGHT => ImGuiKey.RightArrow,
            SDL_Keycode.SDLK_UP => ImGuiKey.UpArrow,
            SDL_Keycode.SDLK_DOWN => ImGuiKey.DownArrow,
            SDL_Keycode.SDLK_PAGEUP => ImGuiKey.PageUp,
            SDL_Keycode.SDLK_PAGEDOWN => ImGuiKey.PageDown,
            SDL_Keycode.SDLK_HOME => ImGuiKey.Home,
            SDL_Keycode.SDLK_END => ImGuiKey.End,
            SDL_Keycode.SDLK_INSERT => ImGuiKey.Insert,
            SDL_Keycode.SDLK_DELETE => ImGuiKey.Delete,
            SDL_Keycode.SDLK_BACKSPACE => ImGuiKey.Backspace,
            SDL_Keycode.SDLK_SPACE => ImGuiKey.Space,
            SDL_Keycode.SDLK_RETURN => ImGuiKey.Enter,
            SDL_Keycode.SDLK_ESCAPE => ImGuiKey.Escape,
            SDL_Keycode.SDLK_APOSTROPHE => ImGuiKey.Apostrophe,
            SDL_Keycode.SDLK_COMMA => ImGuiKey.Comma,
            SDL_Keycode.SDLK_MINUS => ImGuiKey.Minus,
            SDL_Keycode.SDLK_PERIOD => ImGuiKey.Period,
            SDL_Keycode.SDLK_SLASH => ImGuiKey.Slash,
            SDL_Keycode.SDLK_SEMICOLON => ImGuiKey.Semicolon,
            SDL_Keycode.SDLK_EQUALS => ImGuiKey.Equal,
            SDL_Keycode.SDLK_LEFTBRACKET => ImGuiKey.LeftBracket,
            SDL_Keycode.SDLK_BACKSLASH => ImGuiKey.Backslash,
            SDL_Keycode.SDLK_RIGHTBRACKET => ImGuiKey.RightBracket,
            SDL_Keycode.SDLK_GRAVE => ImGuiKey.GraveAccent,
            SDL_Keycode.SDLK_CAPSLOCK => ImGuiKey.CapsLock,
            SDL_Keycode.SDLK_SCROLLLOCK => ImGuiKey.ScrollLock,
            SDL_Keycode.SDLK_NUMLOCKCLEAR => ImGuiKey.NumLock,
            SDL_Keycode.SDLK_PRINTSCREEN => ImGuiKey.PrintScreen,
            SDL_Keycode.SDLK_PAUSE => ImGuiKey.Pause,
            SDL_Keycode.SDLK_LCTRL => ImGuiKey.LeftCtrl,
            SDL_Keycode.SDLK_LSHIFT => ImGuiKey.LeftShift,
            SDL_Keycode.SDLK_LALT => ImGuiKey.LeftAlt,
            SDL_Keycode.SDLK_LGUI => ImGuiKey.LeftSuper,
            SDL_Keycode.SDLK_RCTRL => ImGuiKey.RightCtrl,
            SDL_Keycode.SDLK_RSHIFT => ImGuiKey.RightShift,
            SDL_Keycode.SDLK_RALT => ImGuiKey.RightAlt,
            SDL_Keycode.SDLK_RGUI => ImGuiKey.RightSuper,
            SDL_Keycode.SDLK_A => ImGuiKey.A,
            SDL_Keycode.SDLK_B => ImGuiKey.B,
            SDL_Keycode.SDLK_C => ImGuiKey.C,
            SDL_Keycode.SDLK_D => ImGuiKey.D,
            SDL_Keycode.SDLK_E => ImGuiKey.E,
            SDL_Keycode.SDLK_F => ImGuiKey.F,
            SDL_Keycode.SDLK_G => ImGuiKey.G,
            SDL_Keycode.SDLK_H => ImGuiKey.H,
            SDL_Keycode.SDLK_I => ImGuiKey.I,
            SDL_Keycode.SDLK_J => ImGuiKey.J,
            SDL_Keycode.SDLK_K => ImGuiKey.K,
            SDL_Keycode.SDLK_L => ImGuiKey.L,
            SDL_Keycode.SDLK_M => ImGuiKey.M,
            SDL_Keycode.SDLK_N => ImGuiKey.N,
            SDL_Keycode.SDLK_O => ImGuiKey.O,
            SDL_Keycode.SDLK_P => ImGuiKey.P,
            SDL_Keycode.SDLK_Q => ImGuiKey.Q,
            SDL_Keycode.SDLK_R => ImGuiKey.R,
            SDL_Keycode.SDLK_S => ImGuiKey.S,
            SDL_Keycode.SDLK_T => ImGuiKey.T,
            SDL_Keycode.SDLK_U => ImGuiKey.U,
            SDL_Keycode.SDLK_V => ImGuiKey.V,
            SDL_Keycode.SDLK_W => ImGuiKey.W,
            SDL_Keycode.SDLK_X => ImGuiKey.X,
            SDL_Keycode.SDLK_Y => ImGuiKey.Y,
            SDL_Keycode.SDLK_Z => ImGuiKey.Z,
            SDL_Keycode.SDLK_0 => ImGuiKey.Key0,
            SDL_Keycode.SDLK_1 => ImGuiKey.Key1,
            SDL_Keycode.SDLK_2 => ImGuiKey.Key2,
            SDL_Keycode.SDLK_3 => ImGuiKey.Key3,
            SDL_Keycode.SDLK_4 => ImGuiKey.Key4,
            SDL_Keycode.SDLK_5 => ImGuiKey.Key5,
            SDL_Keycode.SDLK_6 => ImGuiKey.Key6,
            SDL_Keycode.SDLK_7 => ImGuiKey.Key7,
            SDL_Keycode.SDLK_8 => ImGuiKey.Key8,
            SDL_Keycode.SDLK_9 => ImGuiKey.Key9,
            SDL_Keycode.SDLK_F1 => ImGuiKey.F1,
            SDL_Keycode.SDLK_F2 => ImGuiKey.F2,
            SDL_Keycode.SDLK_F3 => ImGuiKey.F3,
            SDL_Keycode.SDLK_F4 => ImGuiKey.F4,
            SDL_Keycode.SDLK_F5 => ImGuiKey.F5,
            SDL_Keycode.SDLK_F6 => ImGuiKey.F6,
            SDL_Keycode.SDLK_F7 => ImGuiKey.F7,
            SDL_Keycode.SDLK_F8 => ImGuiKey.F8,
            SDL_Keycode.SDLK_F9 => ImGuiKey.F9,
            SDL_Keycode.SDLK_F10 => ImGuiKey.F10,
            SDL_Keycode.SDLK_F11 => ImGuiKey.F11,
            SDL_Keycode.SDLK_F12 => ImGuiKey.F12,
            _ => ImGuiKey.None
        };
    }
}
