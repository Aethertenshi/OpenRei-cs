namespace reistar.Renderer.SDL3;

using System;
using System.Collections.Generic;
using SDL;
using reistar.Core;
using reistar.Graphics;
using reistar.Maths;

public unsafe class SdlGpuRenderer : IRenderer, IDisposable
{
    private readonly SdlWindow _window;
    private SDL_GPUDevice* _device;
    private RenderCommand[] _commandBuffer = new RenderCommand[1024];
    private int _commandCount = 0;
    private uint _submissionCounter = 0;

    private Vertex2D[] _vertexBuffer = new Vertex2D[4096];
    private ushort[] _indexBuffer = new ushort[6144];

    public SdlWindow Window => _window;
    public Vect2D CanvasSize => _window.Size;

    public SdlGpuRenderer(string title = "ReiStar Game", int width = 1280, int height = 720)
        : this(new SdlWindow(title, width, height))
    {
    }

    public SdlGpuRenderer(SdlWindow window)
    {
        _window = window;

        _device = SDL3.SDL_CreateGPUDevice(
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV |
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL |
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXBC |
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL,
            true,
            (byte*)null
        );

        if (_device == null)
        {
            throw new InvalidOperationException($"Failed to create SDL_GPU device: {SDL3.SDL_GetError()}");
        }

        if (!SDL3.SDL_ClaimWindowForGPUDevice(_device, window.Handle))
        {
            throw new InvalidOperationException($"Failed to claim window for SDL_GPU: {SDL3.SDL_GetError()}");
        }
    }

    public void BeginFrame()
    {
        _commandCount = 0;
        _submissionCounter = 0;
    }

    public void DrawRect(Vect2D position, Vect2D size, Color color, int zIndex = 0)
    {
        EnsureCapacity();
        ulong key = ((ulong)(zIndex + (long)int.MaxValue) << 32) | _submissionCounter++;
        _commandBuffer[_commandCount++] = new RenderCommand
        {
            SortKey = key,
            Type = RenderPrimitiveType.Rectangle,
            Position = position,
            Size = size,
            Color = color
        };
    }

    public void DrawRectOutline(Vect2D position, Vect2D size, float thickness, Color color, int zIndex = 0)
    {
        EnsureCapacity();
        ulong key = ((ulong)(zIndex + (long)int.MaxValue) << 32) | _submissionCounter++;
        _commandBuffer[_commandCount++] = new RenderCommand
        {
            SortKey = key,
            Type = RenderPrimitiveType.RectangleOutline,
            Position = position,
            Size = size,
            Color = color,
            Thickness = thickness
        };
    }

    public void DrawCircle(Vect2D center, float radius, Color color, int zIndex = 0) { }
    public void DrawLine(Vect2D start, Vect2D end, float thickness, Color color, int zIndex = 0) { }
    public void DrawTexture(ITexture texture, Vect2D position, Vect2D size, Color tint, int zIndex = 0) { }

    public void EndFrame()
    {
        if (_commandCount == 0)
        {
            RenderClearOnly();
            return;
        }

        // 1. Sort commands by ZIndex & submission order
        Array.Sort(_commandBuffer, 0, _commandCount, CommandComparer.Instance);

        // 2. Batch vertices into contiguous memory buffer
        int vertexCount = 0;
        int indexCount = 0;
        float canvasW = CanvasSize.X <= 0 ? 1280f : CanvasSize.X;
        float canvasH = CanvasSize.Y <= 0 ? 720f : CanvasSize.Y;

        EnsureVertexCapacity(_commandCount * 4, _commandCount * 6);

        for (int i = 0; i < _commandCount; i++)
        {
            ref readonly var cmd = ref _commandBuffer[i];
            if (cmd.Type == RenderPrimitiveType.Rectangle)
            {
                float x0 = (cmd.Position.X / canvasW) * 2.0f - 1.0f;
                float y0 = 1.0f - (cmd.Position.Y / canvasH) * 2.0f;
                float x1 = ((cmd.Position.X + cmd.Size.X) / canvasW) * 2.0f - 1.0f;
                float y1 = 1.0f - ((cmd.Position.Y + cmd.Size.Y) / canvasH) * 2.0f;

                float r = cmd.Color.R / 255.0f;
                float g = cmd.Color.G / 255.0f;
                float b = cmd.Color.B / 255.0f;
                float a = cmd.Color.A / 255.0f;

                ushort baseIdx = (ushort)vertexCount;

                _vertexBuffer[vertexCount++] = new Vertex2D(x0, y0, r, g, b, a); // Top-Left
                _vertexBuffer[vertexCount++] = new Vertex2D(x1, y0, r, g, b, a); // Top-Right
                _vertexBuffer[vertexCount++] = new Vertex2D(x1, y1, r, g, b, a); // Bottom-Right
                _vertexBuffer[vertexCount++] = new Vertex2D(x0, y1, r, g, b, a); // Bottom-Left

                _indexBuffer[indexCount++] = baseIdx;
                _indexBuffer[indexCount++] = (ushort)(baseIdx + 1);
                _indexBuffer[indexCount++] = (ushort)(baseIdx + 2);
                _indexBuffer[indexCount++] = (ushort)(baseIdx + 2);
                _indexBuffer[indexCount++] = (ushort)(baseIdx + 3);
                _indexBuffer[indexCount++] = baseIdx;
            }
        }

        // 3. Dispatch GPU render pass
        SDL_GPUCommandBuffer* cmdBuf = SDL3.SDL_AcquireGPUCommandBuffer(_device);
        if (cmdBuf == null) return;

        SDL_GPUTexture* swapchainTexture;
        uint width, height;
        if (!SDL3.SDL_AcquireGPUSwapchainTexture(cmdBuf, _window.Handle, &swapchainTexture, &width, &height))
        {
            SDL3.SDL_SubmitGPUCommandBuffer(cmdBuf);
            return;
        }

        if (swapchainTexture != null)
        {
            SDL_GPUColorTargetInfo targetInfo = new SDL_GPUColorTargetInfo
            {
                texture = swapchainTexture,
                clear_color = new SDL_FColor { r = 0.1f, g = 0.1f, b = 0.18f, a = 1.0f },
                load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
                store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE
            };

            SDL_GPURenderPass* pass = SDL3.SDL_BeginGPURenderPass(cmdBuf, &targetInfo, 1, null);
            if (pass != null)
            {
                SDL3.SDL_EndGPURenderPass(pass);
            }
        }

        SDL3.SDL_SubmitGPUCommandBuffer(cmdBuf);
    }

    private void RenderClearOnly()
    {
        SDL_GPUCommandBuffer* cmdBuf = SDL3.SDL_AcquireGPUCommandBuffer(_device);
        if (cmdBuf == null) return;

        SDL_GPUTexture* swapchainTexture;
        uint width, height;
        if (SDL3.SDL_AcquireGPUSwapchainTexture(cmdBuf, _window.Handle, &swapchainTexture, &width, &height) && swapchainTexture != null)
        {
            SDL_GPUColorTargetInfo targetInfo = new SDL_GPUColorTargetInfo
            {
                texture = swapchainTexture,
                clear_color = new SDL_FColor { r = 0.1f, g = 0.1f, b = 0.18f, a = 1.0f },
                load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
                store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE
            };

            SDL_GPURenderPass* pass = SDL3.SDL_BeginGPURenderPass(cmdBuf, &targetInfo, 1, null);
            if (pass != null)
            {
                SDL3.SDL_EndGPURenderPass(pass);
            }
        }

        SDL3.SDL_SubmitGPUCommandBuffer(cmdBuf);
    }

    private void EnsureCapacity()
    {
        if (_commandCount >= _commandBuffer.Length)
        {
            Array.Resize(ref _commandBuffer, _commandBuffer.Length * 2);
        }
    }

    private void EnsureVertexCapacity(int requiredVerts, int requiredIndices)
    {
        if (requiredVerts > _vertexBuffer.Length)
        {
            Array.Resize(ref _vertexBuffer, Math.Max(requiredVerts, _vertexBuffer.Length * 2));
        }
        if (requiredIndices > _indexBuffer.Length)
        {
            Array.Resize(ref _indexBuffer, Math.Max(requiredIndices, _indexBuffer.Length * 2));
        }
    }

    public void Dispose()
    {
        if (_device != null)
        {
            SDL3.SDL_ReleaseWindowFromGPUDevice(_device, _window.Handle);
            SDL3.SDL_DestroyGPUDevice(_device);
            _device = null;
        }
        _window.Dispose();
    }

    private sealed class CommandComparer : IComparer<RenderCommand>
    {
        public static readonly CommandComparer Instance = new();
        public int Compare(RenderCommand x, RenderCommand y) => x.SortKey.CompareTo(y.SortKey);
    }
}
