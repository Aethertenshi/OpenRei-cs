namespace ReiStar.VisualEditor.State;

using System;
using System.IO;
using ReiStar.VisualEditor.Mounting;

/// <summary>
/// Shared contextual data available to all panels in the Visual Editor.
/// </summary>
public class EditorContext
{
    public EngineHost Host { get; }
    public EditorSelection Selection { get; } = new();
    public string ProjectPath { get; set; }
    public bool ShowImGuiDemo { get; set; } = false;
    public bool RequestLayoutReset { get; set; } = false;

    public EditorContext(EngineHost host, string? projectPath = null)
    {
        Host = host;
        ProjectPath = projectPath ?? Directory.GetCurrentDirectory();
    }
}
