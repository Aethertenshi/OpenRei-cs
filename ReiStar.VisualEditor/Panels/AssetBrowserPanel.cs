namespace ReiStar.VisualEditor.Panels;

using System;
using System.IO;
using System.Numerics;
using Hexa.NET.ImGui;
using ReiStar.VisualEditor.State;

/// <summary>
/// Bottom Asset Browser panel for exploring project files and assets.
/// </summary>
public class AssetBrowserPanel : IEditorPanel
{
    public string Title => "Asset Browser";
    public bool IsOpen { get; set; } = true;

    private string _currentDirectory = string.Empty;

    public void Render(EditorContext context)
    {
        if (!IsOpen) return;

        if (string.IsNullOrEmpty(_currentDirectory))
        {
            _currentDirectory = context.ProjectPath;
        }

        if (ImGui.Begin(Title))
        {
            // Directory Path Bar & Navigation
            ImGui.BeginDisabled(!Directory.Exists(_currentDirectory) || Path.GetDirectoryName(_currentDirectory) == null);
            if (ImGui.Button("< Back") && Path.GetDirectoryName(_currentDirectory) is string parentDir)
            {
                _currentDirectory = parentDir;
            }
            ImGui.EndDisabled();

            ImGui.SameLine();
            ImGui.TextDisabled($"Path: {_currentDirectory}");
            ImGui.Separator();

            if (Directory.Exists(_currentDirectory))
            {
                // Subdirectories
                foreach (var dir in Directory.GetDirectories(_currentDirectory))
                {
                    string dirName = Path.GetFileName(dir);
                    if (dirName.StartsWith(".")) continue;

                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.85f, 0.4f, 1f));
                    bool clicked = ImGui.Selectable($"📁 {dirName}/", false);
                    ImGui.PopStyleColor();

                    if (clicked && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        _currentDirectory = dir;
                        break;
                    }
                }

                // Files
                foreach (var file in Directory.GetFiles(_currentDirectory))
                {
                    string fileName = Path.GetFileName(file);
                    string ext = Path.GetExtension(file).ToLowerInvariant();

                    string icon = ext switch
                    {
                        ".png" or ".jpg" or ".jpeg" or ".bmp" => "🖼️",
                        ".ttf" or ".otf" => "🔤",
                        ".cs" => "📜",
                        ".sln" or ".slnx" or ".csproj" => "📦",
                        _ => "📄"
                    };

                    bool isSelected = ReferenceEquals(context.Selection.SelectedObject, file);
                    if (ImGui.Selectable($"{icon} {fileName}", isSelected))
                    {
                        context.Selection.Select(file);
                    }
                }
            }
            else
            {
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Directory not found.");
            }
        }

        ImGui.End();
    }
}
