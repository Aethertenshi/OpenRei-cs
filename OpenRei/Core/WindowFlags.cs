namespace OpenRei.Core;

[Flags]
public enum WindowFlags
{
    Default = 0,
    Resizable = 1 << 0,
    Fullscreen = 1 << 1,
    Borderless = 1 << 2,
    HighDPI = 1 << 3,
    VSync = 1 << 4,
    Hidden = 1 << 5
}
