namespace ReiStar.VisualEditor.State;

using System;
using reistar.Core;
using reistar.Points.UI;

/// <summary>
/// Tracks current selection across Hierarchy, Inspector, and Viewport panels.
/// </summary>
public class EditorSelection
{
    private object? _selectedObject;

    public object? SelectedObject => _selectedObject;
    public IPoint? SelectedPoint => _selectedObject as IPoint;
    public UIElement? SelectedUIElement => _selectedObject as UIElement;
    public string? SelectedAssetPath => _selectedObject as string;

    public event Action<object?>? SelectionChanged;

    public void Select(object? obj)
    {
        if (!ReferenceEquals(_selectedObject, obj))
        {
            _selectedObject = obj;
            SelectionChanged?.Invoke(_selectedObject);
        }
    }

    public void Clear()
    {
        Select(null);
    }
}
