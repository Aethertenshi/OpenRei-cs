namespace ReiStar.VisualEditor.Panels;

using ReiStar.VisualEditor.State;

/// <summary>
/// Base contract for all modular ImGui panels in the Visual Editor.
/// </summary>
public interface IEditorPanel
{
    string Title { get; }
    bool IsOpen { get; set; }
    void Render(EditorContext context);
}
