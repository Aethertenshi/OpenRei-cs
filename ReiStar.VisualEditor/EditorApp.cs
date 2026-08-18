namespace ReiStar.VisualEditor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Hexa.NET.ImGui;
using SDL;
using reistar.Core;
using reistar.Graphics;
using reistar.Maths;
using reistar.Points.UI;
using reistar.Renderer.SDL3;
using reistar.Shapes;
using ReiStar.VisualEditor.Backend;
using ReiStar.VisualEditor.Mounting;
using ReiStar.VisualEditor.Panels;
using ReiStar.VisualEditor.State;

/// <summary>
/// Main application host for the OpenReiStar Visual Editor.
/// Manages windowing, SDL3 renderer, ImGui docking workspace, and mounted engine scenes.
/// </summary>
public unsafe class EditorApp : IDisposable
{
    private readonly SdlWindow _window;
    private readonly SdlRenderer _renderer;
    private readonly ImGuiSdlRenderer _imguiRenderer;
    private readonly EngineHost _engineHost;
    private readonly EditorContext _context;
    private ImGuiContextPtr _imGuiContext;

    private readonly MenuBarPanel _menuBar = new();
    private readonly List<IEditorPanel> _panels = new();
    private bool _isRunning = true;
    private bool _isDisposed;

    public EditorApp(string title = "OpenReiStar Visual Editor", int width = 1600, int height = 900)
    {
        _window = new SdlWindow(title, width, height, SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
        _renderer = new SdlRenderer(_window);

        // 1. Initialize Dear ImGui
        _imGuiContext = ImGui.CreateContext();
        ImGui.SetCurrentContext(_imGuiContext);

        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;

        ConfigureEditorTheme();

        // 2. Initialize pure C# SDL3 Backends
        ImGuiSdl3Input.Initialize((nint)_window.Handle);
        _imguiRenderer = new ImGuiSdlRenderer(_renderer.Handle);

        // 3. Initialize Engine Host & Default Scene
        _engineHost = new EngineHost(_renderer);
        _engineHost.LoadGame(new EditorDefaultGame(_engineHost.VirtualWindow, _renderer));

        _context = new EditorContext(_engineHost);

        // 4. Register modular panels
        _panels.Add(new ViewportPanel());
        _panels.Add(new HierarchyPanel());
        _panels.Add(new InspectorPanel());
        _panels.Add(new AssetBrowserPanel());
    }

    public void Run()
    {
        while (_isRunning && _window.IsRunning && _engineHost.VirtualWindow.IsRunning)
        {
            // 1. Event Polling
            SDL_Event ev;
            while (SDL3.SDL_PollEvent(&ev))
            {
                if (ev.type == (uint)SDL_EventType.SDL_EVENT_QUIT)
                {
                    _isRunning = false;
                    break;
                }

                ImGuiSdl3Input.ProcessEvent(in ev);
            }

            if (!_isRunning) break;

            // 2. ImGui Frame Setup
            int w = (int)_window.Size.X;
            int h = (int)_window.Size.Y;
            ImGuiSdl3Input.NewFrame(w, h);
            ImGui.NewFrame();

            // 3. Render Top Main Menu Bar
            _menuBar.Render(_context);

            // 4. Create Fullscreen Root Dockspace Host Window
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize);
            ImGui.SetNextWindowViewport(viewport.ID);

            ImGuiWindowFlags hostWindowFlags = ImGuiWindowFlags.NoDocking
                                             | ImGuiWindowFlags.NoTitleBar
                                             | ImGuiWindowFlags.NoCollapse
                                             | ImGuiWindowFlags.NoResize
                                             | ImGuiWindowFlags.NoMove
                                             | ImGuiWindowFlags.NoBringToFrontOnFocus
                                             | ImGuiWindowFlags.NoNavFocus
                                             | ImGuiWindowFlags.NoBackground;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));

            ImGui.Begin("EditorDockspaceHost", hostWindowFlags);
            ImGui.PopStyleVar(3);

            uint dockspaceId = ImGui.GetID("EditorMainDockspace");

            var node = ImGuiP.DockBuilderGetNode(dockspaceId);
            if (node.IsNull || _context.RequestLayoutReset)
            {
                _context.RequestLayoutReset = false;

                ImGuiP.DockBuilderRemoveNode(dockspaceId);
                ImGuiP.DockBuilderAddNode(dockspaceId, ImGuiDockNodeFlags.None);
                ImGuiP.DockBuilderSetNodeSize(dockspaceId, viewport.WorkSize);
                ImGuiP.DockBuilderSetNodePos(dockspaceId, viewport.WorkPos);

                uint centerNode = dockspaceId;
                uint leftNode = 0;
                uint rightNode = 0;
                uint bottomNode = 0;

                // Standard Engine Layout:
                // Left 22%: Hierarchy
                // Right 25%: Inspector
                // Bottom 28%: Asset Browser
                // Center: Scene Viewport
                ImGuiP.DockBuilderSplitNode(centerNode, ImGuiDir.Left, 0.22f, &leftNode, &centerNode);
                ImGuiP.DockBuilderSplitNode(centerNode, ImGuiDir.Right, 0.25f, &rightNode, &centerNode);
                ImGuiP.DockBuilderSplitNode(centerNode, ImGuiDir.Down, 0.28f, &bottomNode, &centerNode);

                ImGuiP.DockBuilderDockWindow("Hierarchy", leftNode);
                ImGuiP.DockBuilderDockWindow("Inspector", rightNode);
                ImGuiP.DockBuilderDockWindow("Asset Browser", bottomNode);
                ImGuiP.DockBuilderDockWindow("Scene Viewport", centerNode);

                ImGuiP.DockBuilderFinish(dockspaceId);
            }

            ImGui.DockSpace(dockspaceId, new Vector2(0, 0), ImGuiDockNodeFlags.PassthruCentralNode);
            ImGui.End();

            // 5. Render Panels
            for (int i = 0; i < _panels.Count; i++)
            {
                _panels[i].Render(_context);
            }

            if (_context.ShowImGuiDemo)
            {
                bool showDemo = _context.ShowImGuiDemo;
                ImGui.ShowDemoWindow(ref showDemo);
                _context.ShowImGuiDemo = showDemo;
            }

            // 5. Render ImGui Draw Lists
            ImGui.Render();

            // Ensure rendering to main window
            SDL3.SDL_SetRenderTarget(_renderer.Handle, null);
            SDL3.SDL_SetRenderDrawColor(_renderer.Handle, 18, 18, 24, 255);
            SDL3.SDL_RenderClear(_renderer.Handle);

            _imguiRenderer.RenderDrawData(ImGui.GetDrawData());

            SDL3.SDL_RenderPresent(_renderer.Handle);
        }
    }

    private void ConfigureEditorTheme()
    {
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();

        style.WindowRounding = 4f;
        style.FrameRounding = 3f;
        style.PopupRounding = 3f;
        style.ScrollbarRounding = 4f;
        style.GrabRounding = 3f;
        style.TabRounding = 3f;

        style.WindowBorderSize = 1f;
        style.FrameBorderSize = 0f;
        style.PopupBorderSize = 1f;

        var colors = style.Colors;
        colors[(int)ImGuiCol.WindowBg] = new Vector4(0.12f, 0.12f, 0.15f, 1.0f);
        colors[(int)ImGuiCol.Header] = new Vector4(0.20f, 0.22f, 0.28f, 1.0f);
        colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.26f, 0.30f, 0.38f, 1.0f);
        colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.30f, 0.35f, 0.45f, 1.0f);
        colors[(int)ImGuiCol.Button] = new Vector4(0.20f, 0.24f, 0.30f, 1.0f);
        colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.28f, 0.34f, 0.44f, 1.0f);
        colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.35f, 0.42f, 0.54f, 1.0f);
        colors[(int)ImGuiCol.FrameBg] = new Vector4(0.16f, 0.17f, 0.22f, 1.0f);
        colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.22f, 0.24f, 0.30f, 1.0f);
        colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.26f, 0.28f, 0.36f, 1.0f);
        colors[(int)ImGuiCol.Tab] = new Vector4(0.15f, 0.16f, 0.20f, 1.0f);
        colors[(int)ImGuiCol.TabHovered] = new Vector4(0.28f, 0.32f, 0.42f, 1.0f);
        colors[(int)ImGuiCol.TabSelected] = new Vector4(0.20f, 0.23f, 0.30f, 1.0f);
        colors[(int)ImGuiCol.TitleBg] = new Vector4(0.10f, 0.10f, 0.13f, 1.0f);
        colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.14f, 0.15f, 0.20f, 1.0f);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _engineHost.Dispose();
            _imguiRenderer.Dispose();
            _renderer.Dispose();
            _window.Dispose();
            if (_imGuiContext.Handle != null)
            {
                ImGui.DestroyContext(_imGuiContext);
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Default interactive scene loaded inside the editor viewport.
/// Demonstrates Layer 3 UIFeaturePoint hierarchy and immediate mode rendering.
/// </summary>
public class EditorDefaultGame : Game
{
    private reistar.Graphics.Font? _font;

    public EditorDefaultGame(IWindow window, IRenderer renderer) : base(window, renderer) { }

    protected override void OnInitialize()
    {
        var ui = Points.AttachPoint(new UIFeaturePoint());

        string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoogleSans-Regular.ttf");
        if (File.Exists(fontPath))
        {
            _font = new reistar.Graphics.Font(fontPath, 28);
        }

        // Demo interactive UI hierarchy
        var mainPanel = new Container
        {
            Id = "MainCanvas",
            Position = UVect.FromScale(0.5f, 0.5f),
            Size = UVect.FromOffset(380, 420),
            Anchor = Anchor.Center,
            BackgroundColor = new reistar.Maths.Color(32, 32, 48, 230),
            BorderColor = new reistar.Maths.Color(70, 80, 110, 255),
            Layout = LayoutMode.VerticalStack,
            Padding = 12f,
            Spacing = 10f
        };

        mainPanel.AddChild(new Label("REISTAR ENGINE", _font, 16f, reistar.Maths.Color.Yellow) { Id = "TitleHeader" });
        mainPanel.AddChild(new Label("Scene Viewport Active", _font, 22f, reistar.Maths.Color.White) { Id = "StatusLabel" });
        mainPanel.AddChild(new Label("1. START GAME", _font, 18f, reistar.Maths.Color.Green) { Id = "MenuItem_1" });
        mainPanel.AddChild(new Label("2. SETTINGS", _font, 18f, reistar.Maths.Color.White) { Id = "MenuItem_2" });
        mainPanel.AddChild(new Label("3. EXIT GAME", _font, 18f, reistar.Maths.Color.Red) { Id = "MenuItem_3" });

        ui.Root.AddChild(mainPanel);
    }

    protected override void OnRender()
    {
        // Viewport background
        Shapes.DrawRect(
            Renderer,
            position: UVect.FromScale(0f, 0f),
            size: UVect.FromScale(1f, 1f),
            color: new reistar.Maths.Color(20, 20, 32, 255),
            zIndex: -10
        );

        if (_font != null)
        {
            Shapes.DrawText(
                Renderer,
                _font,
                "OpenReiStar Viewport Scene",
                position: UVect.FromScale(0.5f, 0.08f),
                fontSize: 24f,
                color: reistar.Maths.Color.White,
                anchor: Anchor.Center,
                zIndex: 10
            );
        }
    }
}
