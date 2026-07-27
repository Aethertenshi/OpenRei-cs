using System.Runtime.InteropServices;

namespace OpenRei.Graphics;

/// <summary>
/// Lock-free, zero-allocation double-buffered native render queue operating on persistent unmanaged memory.
/// </summary>
public unsafe class RenderQueue : IDisposable
{
    private const int DefaultCapacity = 65536; // 64k instances = 2MB persistent buffer

    private QuadInstance* _bufferA;
    private QuadInstance* _bufferB;
    private int _countA;
    private int _countB;
    private bool _writeToA = true;
    private bool _isDisposed;

    public int Capacity { get; }

    public RenderQueue(int capacity = DefaultCapacity)
    {
        Capacity = capacity;
        nuint byteSize = (nuint)(sizeof(QuadInstance) * capacity);
        _bufferA = (QuadInstance*)NativeMemory.Alloc(byteSize);
        _bufferB = (QuadInstance*)NativeMemory.Alloc(byteSize);
    }

    public QuadInstance* ActiveWriteBuffer => _writeToA ? _bufferA : _bufferB;
    public QuadInstance* ActiveReadBuffer => _writeToA ? _bufferB : _bufferA;

    public int ActiveReadCount => _writeToA ? _countB : _countA;

    public void Enqueue(in QuadInstance instance)
    {
        if (_writeToA)
        {
            if (_countA < Capacity) _bufferA[_countA++] = instance;
        }
        else
        {
            if (_countB < Capacity) _bufferB[_countB++] = instance;
        }
    }

    /// <summary>
    /// Swaps write and read buffers atomically between logic and render passes.
    /// </summary>
    public void SwapBuffers()
    {
        if (_writeToA)
        {
            _countB = 0;
            _writeToA = false;
        }
        else
        {
            _countA = 0;
            _writeToA = true;
        }
    }

    public void Clear()
    {
        if (_writeToA) _countA = 0;
        else _countB = 0;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_bufferA != null) NativeMemory.Free(_bufferA);
            if (_bufferB != null) NativeMemory.Free(_bufferB);
            _bufferA = null;
            _bufferB = null;
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
