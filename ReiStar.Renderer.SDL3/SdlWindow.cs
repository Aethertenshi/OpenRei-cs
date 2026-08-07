namespace reistar.Renderer.SDL3;

using SDL;
using reistar.Core;
using reistar.Maths;

public unsafe class SdlWindow : IWindow
{
    private static int _activeWindowCount = 0;
    private SDL_Window* _window;
    private string _title;
    private bool _isRunning;
    private Vect2D _size;

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            if (_window != null)
            {
                SDL3.SDL_SetWindowTitle(_window, _title);
            }
        }
    }

    public Vect2D Size => _size;
    public bool IsRunning => _isRunning;
    public SDL_Window* Handle => _window;

    public SdlWindow(string title, int width, int height, SDL_WindowFlags flags = SDL_WindowFlags.SDL_WINDOW_RESIZABLE)
    {
        _title = title;
        _size = new Vect2D(width, height);

        if (_activeWindowCount == 0)
        {
            if (!SDL3.SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
            {
                throw new InvalidOperationException($"Failed to initialize SDL3: {SDL3.SDL_GetError()}");
            }
            if (!SDL3_ttf.TTF_Init())
            {
                throw new InvalidOperationException($"Failed to initialize SDL3_ttf: {SDL3.SDL_GetError()}");
            }
        }
        _activeWindowCount++;

        _window = SDL3.SDL_CreateWindow(title, width, height, flags);
        if (_window == null)
        {
            _activeWindowCount--;
            throw new InvalidOperationException($"Failed to create SDL3 Window: {SDL3.SDL_GetError()}");
        }

        _isRunning = true;
    }

    public void PollEvents()
    {
        SDL_Event ev;
        while (SDL3.SDL_PollEvent(&ev))
        {
            if (ev.type == (uint)SDL_EventType.SDL_EVENT_QUIT)
            {
                _isRunning = false;
            }
            else if (ev.type == (uint)SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED)
            {
                _size = new Vect2D(ev.window.data1, ev.window.data2);
            }
        }
    }

    public void Dispose()
    {
        if (_window != null)
        {
            SDL3.SDL_DestroyWindow(_window);
            _window = null;
            _activeWindowCount--;

            if (_activeWindowCount <= 0)
            {
                _activeWindowCount = 0;
                SDL3_ttf.TTF_Quit();
                SDL3.SDL_Quit();
            }
        }
    }
}

