using OpenRei.Types;

namespace OpenRei.InputSystem;

/// <summary>
/// Event-driven input manager providing Begin, Hold, and Ended input events.
/// </summary>
public static class Input
{
    private static readonly HashSet<KeyType> ActiveKeys = new();

    /// <summary>
    /// Fired once when a key or button is pressed down.
    /// </summary>
    public static event Action<KeyType>? Begin;

    /// <summary>
    /// Fired continuously every frame while a key or button is held down.
    /// </summary>
    public static event Action<KeyType, float>? Hold;

    /// <summary>
    /// Fired once when a key or button is released.
    /// </summary>
    public static event Action<KeyType>? Ended;

    /// <summary>
    /// Fired when the mouse wheel is scrolled. Delta is screen-relative (positive = scroll up/left).
    /// </summary>
    public static event Action<Vector2D>? MouseWheel;

    public static Vector2D MousePosition { get; set; } = Vector2D.Zero;
    public static Vector2D MouseWheelDelta { get; set; } = Vector2D.Zero;

    public static bool IsKeyDown(KeyType key) => ActiveKeys.Contains(key);

    public static void TriggerBegin(KeyType key)
    {
        if (ActiveKeys.Add(key))
        {
            Begin?.Invoke(key);
        }
    }

    public static void TriggerHold(float deltaTime)
    {
        foreach (var key in ActiveKeys)
        {
            Hold?.Invoke(key, deltaTime);
        }
    }

    public static void TriggerEnded(KeyType key)
    {
        if (ActiveKeys.Remove(key))
        {
            Ended?.Invoke(key);
        }
    }

    public static void TriggerMouseWheel(Vector2D delta)
    {
        MouseWheelDelta = new Vector2D(MouseWheelDelta.X + delta.X, MouseWheelDelta.Y + delta.Y);
        MouseWheel?.Invoke(delta);
    }

    public static void ResetMouseWheelDelta()
    {
        MouseWheelDelta = Vector2D.Zero;
    }
}
