namespace ReiStar.VisualEditor.Panels;

using System;
using System.Numerics;
using Hexa.NET.ImGui;
using reistar.Core;
using reistar.Maths;
using reistar.Points.UI;
using ReiStar.VisualEditor.State;

/// <summary>
/// Left Hierarchy panel displaying the scene graph and UI tree.
/// </summary>
public class HierarchyPanel : IEditorPanel
{
    public string Title => "Hierarchy";
    public bool IsOpen { get; set; } = true;

    public void Render(EditorContext context)
    {
        if (!IsOpen) return;

        if (ImGui.Begin(Title))
        {
            var game = context.Host.ActiveGame;
            if (game == null)
            {
                ImGui.TextDisabled("No active game instance loaded.");
                ImGui.End();
                return;
            }

            ImGui.TextDisabled("Scene Root");
            ImGui.Separator();

            // Render UIFeaturePoint tree
            var uiPoint = game.Points.GetPoint<UIFeaturePoint>();
            if (uiPoint != null)
            {
                ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.OpenOnArrow;
                if (ReferenceEquals(context.Selection.SelectedObject, uiPoint))
                {
                    flags |= ImGuiTreeNodeFlags.Selected;
                }

                bool nodeOpen = ImGui.TreeNodeEx($"UI Feature Point ({uiPoint.Name})", flags);
                if (ImGui.IsItemClicked())
                {
                    context.Selection.Select(uiPoint);
                }

                if (nodeOpen)
                {
                    RenderUIElementNode(uiPoint.Root, context);
                    ImGui.TreePop();
                }
            }

            // Right-click empty area context menu
            if (ImGui.BeginPopupContextWindow("HierarchyContext", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                if (uiPoint != null && ImGui.MenuItem("Add Container to Root"))
                {
                    var container = new Container(UVect.FromScale(0.5f, 0.5f), UVect.FromOffset(200, 200), new Color(40, 40, 60, 200))
                    {
                        Id = $"Container_{uiPoint.Root.Children.Count + 1}",
                        Anchor = Anchor.Center
                    };
                    uiPoint.Root.AddChild(container);
                    context.Selection.Select(container);
                }
                ImGui.EndPopup();
            }
        }

        ImGui.End();
    }

    private void RenderUIElementNode(UIElement element, EditorContext context)
    {
        string typeName = element switch
        {
            Label => "Label",
            Image => "Image",
            Container => "Container",
            _ => element.GetType().Name
        };

        string label = string.IsNullOrEmpty(element.Id)
            ? $"[{typeName}]"
            : $"[{typeName}] {element.Id}";

        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (element.Children.Count == 0)
        {
            flags |= ImGuiTreeNodeFlags.Leaf;
        }
        else
        {
            flags |= ImGuiTreeNodeFlags.DefaultOpen;
        }

        if (ReferenceEquals(context.Selection.SelectedObject, element))
        {
            flags |= ImGuiTreeNodeFlags.Selected;
        }

        bool isOpen = ImGui.TreeNodeEx($"{label}##{element.GetHashCode()}", flags);
        if (ImGui.IsItemClicked())
        {
            context.Selection.Select(element);
        }

        // Element Context Menu
        if (ImGui.BeginPopupContextItem($"ItemContext_{element.GetHashCode()}"))
        {
            if (ImGui.MenuItem("Add Child Container"))
            {
                var newChild = new Container(UVect.FromOffset(0, 0), UVect.FromOffset(100, 50), new Color(50, 50, 70, 255))
                {
                    Id = $"Child_{element.Children.Count + 1}"
                };
                element.AddChild(newChild);
                context.Selection.Select(newChild);
            }

            if (ImGui.MenuItem("Add Child Label"))
            {
                var newLabel = new Label("New Label", null, 18f, Color.White)
                {
                    Id = $"Label_{element.Children.Count + 1}"
                };
                element.AddChild(newLabel);
                context.Selection.Select(newLabel);
            }

            if (element.Parent != null)
            {
                ImGui.Separator();
                if (ImGui.MenuItem("Delete Element"))
                {
                    if (ReferenceEquals(context.Selection.SelectedObject, element))
                    {
                        context.Selection.Clear();
                    }
                    element.Parent.RemoveChild(element);
                }
            }

            ImGui.EndPopup();
        }

        if (isOpen)
        {
            for (int i = 0; i < element.Children.Count; i++)
            {
                RenderUIElementNode(element.Children[i], context);
            }
            ImGui.TreePop();
        }
    }
}
