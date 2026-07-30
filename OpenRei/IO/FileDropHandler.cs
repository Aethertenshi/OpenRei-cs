using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SDL;

namespace OpenRei.IO;

/// <summary>
/// Hooks into native SDL3 drag-and-drop file events and queues dropped file paths for main-thread ingestion.
/// </summary>
public static class FileDropHandler
{
    private static readonly ConcurrentQueue<string> _pending = new();
    private static bool _initialized;

    /// <summary>
    /// Event triggered when any file is dropped onto the window.
    /// Guaranteed to fire exactly ONCE per file drop.
    /// </summary>
    public static event Action<string>? OnFileDropped;

    /// <summary>
    /// Registers the SDL3 drop-file event watcher. Call once after SDL window creation.
    /// </summary>
    public static unsafe void Initialize()
    {
        if (_initialized) return;

        SDL3.SDL_SetEventEnabled((uint)SDL_EventType.SDL_EVENT_DROP_FILE, true);

        _initialized = true;
        Console.WriteLine("[FileDropHandler] SDL3 Drag-and-Drop file listener initialized successfully.");
    }

    /// <summary>
    /// Removes the SDL3 event watcher.
    /// </summary>
    public static unsafe void Shutdown()
    {
        if (!_initialized) return;

        _initialized = false;
    }

    /// <summary>
    /// Enqueues a file drop path and triggers OnFileDropped exactly once on the main thread.
    /// </summary>
    public static void Enqueue(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            _pending.Enqueue(path);
            OnFileDropped?.Invoke(path);
        }
    }

    /// <summary>
    /// Drains queued file paths on the main thread and invokes processing callbacks.
    /// </summary>
    public static void DrainQueue(Action<string> onFile)
    {
        while (_pending.TryDequeue(out string? path))
        {
            if (!string.IsNullOrEmpty(path))
            {
                onFile(path);
            }
        }
    }
}
