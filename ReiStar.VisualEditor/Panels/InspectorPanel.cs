namespace ReiStar.VisualEditor.Panels;

using System;
using System.Numerics;
using Hexa.NET.ImGui;
using reistar.Core;
using reistar.Maths;
using reistar.Points.UI;
using ReiStar.VisualEditor.State;

/// <summary>
/// Right Inspector panel for inspecting and modifying properties of selected elements.
/// </summary>
public class InspectorPanel : IEditorPanel
{
    public string Title => "Inspector";
    public bool IsOpen { get; set; } = true;

    public void Render(EditorContext context)
    {
        if (!IsOpen) return;

        if (ImGui.Begin(Title))
        {
            var selected = context.Selection.SelectedObject;

            if (selected == null)
            {
                ImGui.TextDisabled("Select an object in Hierarchy to inspect.");
                ImGui.End();
                return;
            }

            if (selected is UIElement element)
            {
                RenderUIElementInspector(element);
            }
            else if (selected is IPoint point)
            {
                RenderPointInspector(point);
            }
            else if (selected is string assetPath)
            {
                RenderAssetInspector(assetPath);
            }
        }

        ImGui.End();
    }

    private void RenderUIElementInspector(UIElement elem)
    {
        ImGui.TextColored(new Vector4(0.3f, 0.8f, 1f, 1f), $"{elem.GetType().Name} Inspector");
        ImGui.Separator();

        // 1. Identification
        string id = elem.Id ?? string.Empty;
        if (ImGui.InputText("ID", ref id, 64u))
        {
            elem.Id = id;
        }

        ImGui.Spacing();

        // 2. Transform & Coordinates
        if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
        {
            // Position (Scale + Offset)
            float posScaleX = elem.Position.ScaleX;
            float posOffX = elem.Position.OffsetX;
            float posScaleY = elem.Position.ScaleY;
            float posOffY = elem.Position.OffsetY;

            bool posChanged = false;
            ImGui.Text("Position");
            ImGui.PushItemWidth(80);
            posChanged |= ImGui.DragFloat("SX##Pos", ref posScaleX, 0.01f);
            ImGui.SameLine();
            posChanged |= ImGui.DragFloat("OX (px)##Pos", ref posOffX, 1.0f);
            posChanged |= ImGui.DragFloat("SY##Pos", ref posScaleY, 0.01f);
            ImGui.SameLine();
            posChanged |= ImGui.DragFloat("OY (px)##Pos", ref posOffY, 1.0f);
            ImGui.PopItemWidth();

            if (posChanged)
            {
                elem.Position = new UVect(posScaleX, posOffX, posScaleY, posOffY);
            }

            ImGui.Spacing();

            // Size (Scale + Offset)
            float sizeScaleX = elem.Size.ScaleX;
            float sizeOffX = elem.Size.OffsetX;
            float sizeScaleY = elem.Size.ScaleY;
            float sizeOffY = elem.Size.OffsetY;

            bool sizeChanged = false;
            ImGui.Text("Size");
            ImGui.PushItemWidth(80);
            sizeChanged |= ImGui.DragFloat("SX##Size", ref sizeScaleX, 0.01f);
            ImGui.SameLine();
            sizeChanged |= ImGui.DragFloat("OX (px)##Size", ref sizeOffX, 1.0f);
            sizeChanged |= ImGui.DragFloat("SY##Size", ref sizeScaleY, 0.01f);
            ImGui.SameLine();
            sizeChanged |= ImGui.DragFloat("OY (px)##Size", ref sizeOffY, 1.0f);
            ImGui.PopItemWidth();

            if (sizeChanged)
            {
                elem.Size = new UVect(sizeScaleX, sizeOffX, sizeScaleY, sizeOffY);
            }

            ImGui.Spacing();

            // Anchor
            float anchorX = elem.Anchor.X;
            float anchorY = elem.Anchor.Y;
            bool anchorChanged = false;
            ImGui.Text("Anchor");
            ImGui.PushItemWidth(80);
            anchorChanged |= ImGui.SliderFloat("X##Anchor", ref anchorX, 0f, 1f, "%.2f");
            ImGui.SameLine();
            anchorChanged |= ImGui.SliderFloat("Y##Anchor", ref anchorY, 0f, 1f, "%.2f");
            ImGui.PopItemWidth();

            // Anchor preset buttons
            if (ImGui.SmallButton("Top-Left")) { elem.Anchor = Anchor.TopLeft; }
            ImGui.SameLine();
            if (ImGui.SmallButton("Center")) { elem.Anchor = Anchor.Center; }
            ImGui.SameLine();
            if (ImGui.SmallButton("Top-Center")) { elem.Anchor = Anchor.TopCenter; }

            if (anchorChanged)
            {
                elem.Anchor = new Anchor(anchorX, anchorY);
            }
        }

        ImGui.Spacing();

        // 3. Styling & Appearance
        if (ImGui.CollapsingHeader("Appearance", ImGuiTreeNodeFlags.DefaultOpen))
        {
            Vector4 bgCol = ColorToVector4(elem.BackgroundColor);
            if (ImGui.ColorEdit4("Background", ref bgCol))
            {
                elem.BackgroundColor = Vector4ToColor(bgCol);
            }

            Vector4 borderCol = ColorToVector4(elem.BorderColor);
            if (ImGui.ColorEdit4("Border", ref borderCol))
            {
                elem.BorderColor = Vector4ToColor(borderCol);
            }

            int zIndex = elem.ZIndex;
            if (ImGui.DragInt("Z-Index", ref zIndex, 1, -100, 100))
            {
                elem.ZIndex = zIndex;
            }
        }

        ImGui.Spacing();

        // 4. Layout Settings
        if (ImGui.CollapsingHeader("Layout Settings"))
        {
            int currentLayout = (int)elem.Layout;
            string[] layoutNames = { "None", "VerticalStack", "HorizontalStack" };
            if (ImGui.Combo("Layout Mode", ref currentLayout, layoutNames, layoutNames.Length))
            {
                elem.Layout = (LayoutMode)currentLayout;
            }

            float padding = elem.Padding;
            if (ImGui.DragFloat("Padding", ref padding, 0.5f, 0f, 100f))
            {
                elem.Padding = padding;
            }

            float spacing = elem.Spacing;
            if (ImGui.DragFloat("Spacing", ref spacing, 0.5f, 0f, 100f))
            {
                elem.Spacing = spacing;
            }
        }

        ImGui.Spacing();

        // 5. Element-specific properties
        if (elem is Label label)
        {
            if (ImGui.CollapsingHeader("Label Properties", ImGuiTreeNodeFlags.DefaultOpen))
            {
                string text = label.Text ?? string.Empty;
                if (ImGui.InputTextMultiline("Text", ref text, 256u, new Vector2(-1, 60)))
                {
                    label.Text = text;
                }

                float fSize = label.FontSize;
                if (ImGui.DragFloat("Font Size", ref fSize, 1.0f, 6f, 120f))
                {
                    label.FontSize = fSize;
                }

                Vector4 textCol = ColorToVector4(label.TextColor);
                if (ImGui.ColorEdit4("Text Color", ref textCol))
                {
                    label.TextColor = Vector4ToColor(textCol);
                }
            }
        }
        else if (elem is Image img)
        {
            if (ImGui.CollapsingHeader("Image Properties", ImGuiTreeNodeFlags.DefaultOpen))
            {
                int currentMode = (int)img.SizeMode;
                string[] modeNames = { "Fill", "Contain", "Cover", "None", "Crop" };
                if (ImGui.Combo("Size Mode", ref currentMode, modeNames, modeNames.Length))
                {
                    img.SizeMode = (ImageSizeMode)currentMode;
                }

                Vector4 tint = ColorToVector4(img.Tint);
                if (ImGui.ColorEdit4("Tint", ref tint))
                {
                    img.Tint = Vector4ToColor(tint);
                }
            }
        }
    }

    private void RenderPointInspector(IPoint point)
    {
        ImGui.TextColored(new Vector4(0.3f, 0.8f, 1f, 1f), "Point Plugin");
        ImGui.Separator();

        ImGui.Text($"Name: {point.Name}");
        bool enabled = point.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            point.Enabled = enabled;
        }
    }

    private void RenderAssetInspector(string assetPath)
    {
        ImGui.TextColored(new Vector4(0.9f, 0.8f, 0.3f, 1f), "Asset File");
        ImGui.Separator();
        ImGui.TextWrapped($"Path: {assetPath}");
    }

    private static Vector4 ColorToVector4(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
    private static Color Vector4ToColor(Vector4 v) => new((byte)(v.X * 255f), (byte)(v.Y * 255f), (byte)(v.Z * 255f), (byte)(v.W * 255f));
}
