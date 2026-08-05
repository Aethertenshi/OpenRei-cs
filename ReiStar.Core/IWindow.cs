namespace reistar.Core;

using reistar.Maths;

public interface IWindow : IDisposable
{
    string Title { get; set; }
    Vect2D Size { get; }
    bool IsRunning { get; }

    void PollEvents();
}
