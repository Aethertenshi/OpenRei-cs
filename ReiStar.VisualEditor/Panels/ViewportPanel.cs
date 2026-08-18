namespace ReiStar.VisualEditor.Panels;

using System;
using System.Numerics;
using Hexa.NET.ImGui;
using reistar.Core;
using ReiStar.VisualEditor.State;

/// <summary>
/// Center Scene Viewport panel displaying the mounted engine output.
/// </summary>
public class ViewportPanel : IEditorPanel
{
    public string Title => "Scene Viewport";
    public bool IsOpen { get; set; } = true;

    public void Render(EditorContext context)
    {
        if (!IsOpen) return;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        bool visible = ImGui.Begin(Title, ref _isOpenField, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar();

        if (visible)
        {
            Vector2 contentSize = ImGui.GetContentRegionAvail();
            int renderW = Math.Max(64, (int)contentSize.X);
            int renderH = Math.Max(64, (int)contentSize.Y);

            // Step engine and render scene into offscreen target
            context.Host.UpdateAndRender(renderW, renderH, ImGui.GetIO().DeltaTime);

            nint texHandle = context.Host.GetRenderTargetTextureHandle();
            if (texHandle != 0)
            {
                unsafe
                {
                    ImTextureRef textureRef = new ImTextureRef(null, new ImTextureID((void*)texHandle));
                    ImGui.Image(textureRef, new Vector2(renderW, renderH), new Vector2(0, 0), new Vector2(1, 1));
                }
            }

            // Viewport HUD overlay (resolution + FPS)
            Vector2 overlayPos = ImGui.GetWindowPos() + new Vector2(10, 30);
            ImGui.SetNextWindowPos(overlayPos, ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.6f);

            var overlayFlags = ImGuiWindowFlags.NoDecoration |
                               ImGuiWindowFlags.AlwaysAutoResize |
                               ImGuiWindowFlags.NoSavedSettings |
                               ImGuiWindowFlags.NoFocusOnAppearing |
                               ImGuiWindowFlags.NoNav |
                               ImGuiWindowFlags.NoMove;

            if (ImGui.Begin("##ViewportOverlay", overlayFlags))
            {
                ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), $"{renderW}x{renderH}");
                ImGui.SameLine();
                ImGui.TextDisabled("|");
                ImGui.SameLine();
                ImGui.Text($"{Time.GetFPS():F0} FPS");
                ImGui.End();
            }
        }

        ImGui.End();
    }

    private bool _isOpenField = true;
}
