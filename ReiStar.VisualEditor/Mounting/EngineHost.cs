namespace ReiStar.VisualEditor.Mounting;

using System;
using reistar.Core;
using reistar.Graphics;
using reistar.Maths;
using reistar.Renderer.SDL3;

public enum PlayState
{
    Stopped,
    Playing,
    Paused
}

/// <summary>
/// Hosts an OpenReiStar Game instance in an offscreen render target texture,
/// providing frame stepping, resizing, and texture handle access for ImGui.
/// </summary>
public unsafe class EngineHost : IDisposable
{
    private readonly EditorViewportWindow _virtualWindow;
    private readonly SdlRenderer _renderer;
    private Game? _activeGame;
    private ITexture? _renderTarget;
    private PlayState _state = PlayState.Playing;
    private bool _isDisposed;

    public EditorViewportWindow VirtualWindow => _virtualWindow;
    public SdlRenderer Renderer => _renderer;
    public Game? ActiveGame => _activeGame;
    public PlayState State => _state;
    public ITexture? RenderTarget => _renderTarget;

    public EngineHost(SdlRenderer renderer)
    {
        _renderer = renderer;
        _virtualWindow = new EditorViewportWindow();
    }

    public void LoadGame(Game game)
    {
        _activeGame?.Dispose();
        _activeGame = game;
        _activeGame.Initialize();
    }

    public void Play()
    {
        _state = PlayState.Playing;
    }

    public void Pause()
    {
        _state = PlayState.Paused;
    }

    public void StepSingleFrame(float delta = 1.0f / 60.0f)
    {
        if (_activeGame != null && _renderTarget != null)
        {
            _renderer.SetRenderTarget(_renderTarget);
            _activeGame.Step(delta);
            _renderer.SetRenderTarget(null);
        }
    }

    public void UpdateAndRender(int targetWidth, int targetHeight, float deltaTime)
    {
        if (_activeGame == null) return;

        int width = Math.Max(64, targetWidth);
        int height = Math.Max(64, targetHeight);

        // Resize render target if viewport dimensions changed
        if (_renderTarget == null || _renderTarget.Width != width || _renderTarget.Height != height)
        {
            _renderTarget?.Dispose();
            _renderTarget = _renderer.CreateRenderTarget(width, height);
            _virtualWindow.SetViewportSize(width, height);
        }

        if (_renderTarget != null)
        {
            _renderer.SetRenderTarget(_renderTarget);

            if (_state == PlayState.Playing)
            {
                _activeGame.Step(deltaTime);
            }
            else
            {
                // In paused/stopped state, keep rendering the scene with 0 delta time
                _activeGame.Step(0f);
            }

            _renderer.SetRenderTarget(null);
        }
    }

    public nint GetRenderTargetTextureHandle()
    {
        if (_renderTarget is SdlTexture sdlTex && sdlTex.Handle != null)
        {
            return (nint)sdlTex.Handle;
        }
        return 0;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _activeGame?.Dispose();
            _activeGame = null;
            _renderTarget?.Dispose();
            _renderTarget = null;
            _virtualWindow.Dispose();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
