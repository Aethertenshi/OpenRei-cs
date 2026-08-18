namespace ReiStar.VisualEditor.Panels;

using System.Numerics;
using Hexa.NET.ImGui;
using ReiStar.VisualEditor.Mounting;
using ReiStar.VisualEditor.State;

/// <summary>
/// Top application menu bar and engine playback controls toolbar.
/// </summary>
public class MenuBarPanel
{
    public void Render(EditorContext context)
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New Scene", "Ctrl+N")) { }
                if (ImGui.MenuItem("Open Scene...", "Ctrl+O")) { }
                ImGui.Separator();
                if (ImGui.MenuItem("Save Scene", "Ctrl+S")) { }
                ImGui.Separator();
                if (ImGui.MenuItem("Exit", "Alt+F4"))
                {
                    context.Host.VirtualWindow.Stop();
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem("Undo", "Ctrl+Z")) { }
                if (ImGui.MenuItem("Redo", "Ctrl+Y")) { }
                ImGui.Separator();
                if (ImGui.MenuItem("Deselect All", "Esc"))
                {
                    context.Selection.Clear();
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("View"))
            {
                if (ImGui.MenuItem("Reset Layout to Default"))
                {
                    context.RequestLayoutReset = true;
                }
                ImGui.Separator();
                bool showDemo = context.ShowImGuiDemo;
                if (ImGui.MenuItem("ImGui Demo Window", "", ref showDemo))
                {
                    context.ShowImGuiDemo = showDemo;
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Help"))
            {
                if (ImGui.MenuItem("About OpenReiStar")) { }
                ImGui.EndMenu();
            }

            // Right-aligned engine status & playback toolbar
            RenderToolbarControls(context);

            ImGui.EndMainMenuBar();
        }
    }

    private void RenderToolbarControls(EditorContext context)
    {
        float barWidth = ImGui.GetWindowWidth();
        float buttonGroupWidth = 220f;
        ImGui.SameLine(barWidth - buttonGroupWidth);

        var state = context.Host.State;

        if (state == PlayState.Playing)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1f));
            if (ImGui.SmallButton("|| Pause"))
            {
                context.Host.Pause();
            }
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.45f, 0.15f, 1f));
            if (ImGui.SmallButton("> Play"))
            {
                context.Host.Play();
            }
            ImGui.PopStyleColor();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(">| Step"))
        {
            context.Host.StepSingleFrame();
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"[{state}]");
    }
}
