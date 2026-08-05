namespace reistar.Core;

using reistar.Graphics;

public interface IPoint
{
    string Name { get; }
    bool Enabled { get; set; }

    void OnAttach(EngineContext context);
    void OnUpdate(float deltaTime);
    void OnRender(IRenderer renderer);
    void OnDetach();
}
