namespace reistar.Core;

using reistar.Graphics;

public sealed class EngineContext
{
    public IWindow Window { get; }
    public IRenderer Renderer { get; }

    public EngineContext(IWindow window, IRenderer renderer)
    {
        Window = window;
        Renderer = renderer;
    }
}
