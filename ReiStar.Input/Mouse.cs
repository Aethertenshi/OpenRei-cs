namespace reistar.Input;

using System;
using reistar.Maths;

public static class Mouse
{
    public static Vect2D Position => Input.MousePosition;
    public static Vect2D Delta => Input.MouseDelta;
    public static float Wheel => Input.MouseWheel;

    public static bool IsDown(MouseButton button = MouseButton.Left) => Input.IsMouseButtonDown(button);
    public static bool IsClicked(MouseButton button = MouseButton.Left) => Input.IsMouseButtonClicked(button);
    public static bool IsReleased(MouseButton button = MouseButton.Left) => Input.IsMouseButtonReleased(button);

    public static void OnDown(MouseButton button, Action<Vect2D> action) => Input.OnMouseDown(button, action);
    public static void OnUp(MouseButton button, Action<Vect2D> action) => Input.OnMouseUp(button, action);
}
