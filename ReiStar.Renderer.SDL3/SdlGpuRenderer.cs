namespace reistar.Renderer.SDL3;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SDL;
using reistar.Core;
using reistar.Graphics;
using reistar.Maths;

public unsafe class SdlGpuRenderer : IRenderer, IDisposable
{
    private readonly SdlWindow _window;
    private SDL_GPUDevice* _device;
    private SDL_GPUGraphicsPipeline* _pipeline;
    private SDL_GPUShader* _vertexShader;
    private SDL_GPUShader* _fragmentShader;

    private SDL_GPUBuffer* _vertexBufferGPU;
    private SDL_GPUBuffer* _indexBufferGPU;
    private uint _vertexBufferSize = 4096 * (uint)sizeof(Vertex2D);
    private uint _indexBufferSize = 6144 * sizeof(ushort);

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
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
            true,
            (byte*)null
        );

        if (_device == null)
        {
            throw new InvalidOperationException($"Failed to create SDL_GPU device with SPIR-V: {SDL3.SDL_GetError()}");
        }

        if (!SDL3.SDL_ClaimWindowForGPUDevice(_device, window.Handle))
        {
            throw new InvalidOperationException($"Failed to claim window for SDL_GPU: {SDL3.SDL_GetError()}");
        }

        InitializePipeline();
    }

    private void InitializePipeline()
    {
        _vertexShader = SpirvShaderLoader.LoadFromBytes(_device, EmbeddedShaders.Color2DVert, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX);
        _fragmentShader = SpirvShaderLoader.LoadFromBytes(_device, EmbeddedShaders.Color2DFrag, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT);

        SDL_GPUColorTargetDescription targetDesc = new SDL_GPUColorTargetDescription
        {
            format = SDL3.SDL_GetGPUSwapchainTextureFormat(_device, _window.Handle)
        };

        SDL_GPUVertexAttribute* attrs = stackalloc SDL_GPUVertexAttribute[2];
        attrs[0] = new SDL_GPUVertexAttribute
        {
            location = 0,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2,
            offset = 0
        };
        attrs[1] = new SDL_GPUVertexAttribute
        {
            location = 1,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4,
            offset = sizeof(float) * 2
        };

        SDL_GPUVertexBufferDescription bufferDesc = new SDL_GPUVertexBufferDescription
        {
            slot = 0,
            pitch = (uint)sizeof(Vertex2D),
            input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
            instance_step_rate = 0
        };

        SDL_GPUGraphicsPipelineCreateInfo pipelineInfo = new SDL_GPUGraphicsPipelineCreateInfo
        {
            vertex_shader = _vertexShader,
            fragment_shader = _fragmentShader,
            primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST,
            rasterizer_state = new SDL_GPURasterizerState
            {
                fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL,
                cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE,
                front_face = SDL_GPUFrontFace.SDL_GPU_FRONTFACE_COUNTER_CLOCKWISE
            },
            target_info = new SDL_GPUGraphicsPipelineTargetInfo
            {
                color_target_descriptions = &targetDesc,
                num_color_targets = 1
            },
            vertex_input_state = new SDL_GPUVertexInputState
            {
                vertex_buffer_descriptions = &bufferDesc,
                num_vertex_buffers = 1,
                vertex_attributes = attrs,
                num_vertex_attributes = 2
            }
        };

        _pipeline = SDL3.SDL_CreateGPUGraphicsPipeline(_device, &pipelineInfo);
        if (_pipeline == null)
        {
            throw new InvalidOperationException($"Failed to create 2D graphics pipeline: {SDL3.SDL_GetError()}");
        }

        SDL3.SDL_ReleaseGPUShader(_device, _vertexShader);
        SDL3.SDL_ReleaseGPUShader(_device, _fragmentShader);
        _vertexShader = null;
        _fragmentShader = null;

        SDL_GPUBufferCreateInfo vertBufferInfo = new SDL_GPUBufferCreateInfo
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX,
            size = _vertexBufferSize
        };
        _vertexBufferGPU = SDL3.SDL_CreateGPUBuffer(_device, &vertBufferInfo);

        SDL_GPUBufferCreateInfo idxBufferInfo = new SDL_GPUBufferCreateInfo
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX,
            size = _indexBufferSize
        };
        _indexBufferGPU = SDL3.SDL_CreateGPUBuffer(_device, &idxBufferInfo);
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

        Array.Sort(_commandBuffer, 0, _commandCount, CommandComparer.Instance);

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

                _vertexBuffer[vertexCount++] = new Vertex2D(x0, y0, r, g, b, a);
                _vertexBuffer[vertexCount++] = new Vertex2D(x1, y0, r, g, b, a);
                _vertexBuffer[vertexCount++] = new Vertex2D(x1, y1, r, g, b, a);
                _vertexBuffer[vertexCount++] = new Vertex2D(x0, y1, r, g, b, a);

                _indexBuffer[indexCount++] = baseIdx;
                _indexBuffer[indexCount++] = (ushort)(baseIdx + 1);
                _indexBuffer[indexCount++] = (ushort)(baseIdx + 2);
                _indexBuffer[indexCount++] = (ushort)(baseIdx + 2);
                _indexBuffer[indexCount++] = (ushort)(baseIdx + 3);
                _indexBuffer[indexCount++] = baseIdx;
            }
        }

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
            UploadBufferData(cmdBuf, vertexCount, indexCount);

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
                SDL3.SDL_BindGPUGraphicsPipeline(pass, _pipeline);

                SDL_GPUBufferBinding vertBinding = new SDL_GPUBufferBinding
                {
                    buffer = _vertexBufferGPU,
                    offset = 0
                };
                SDL3.SDL_BindGPUVertexBuffers(pass, 0, &vertBinding, 1);

                SDL_GPUBufferBinding idxBinding = new SDL_GPUBufferBinding
                {
                    buffer = _indexBufferGPU,
                    offset = 0
                };
                SDL3.SDL_BindGPUIndexBuffer(pass, &idxBinding, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_16BIT);

                SDL3.SDL_DrawGPUIndexedPrimitives(pass, (uint)indexCount, 1, 0, 0, 0);

                SDL3.SDL_EndGPURenderPass(pass);
            }
        }

        SDL3.SDL_SubmitGPUCommandBuffer(cmdBuf);
    }

    private void UploadBufferData(SDL_GPUCommandBuffer* cmdBuf, int vertexCount, int indexCount)
    {
        uint vertBytes = (uint)(vertexCount * sizeof(Vertex2D));
        uint idxBytes = (uint)(indexCount * sizeof(ushort));

        SDL_GPUTransferBufferCreateInfo xferInfo = new SDL_GPUTransferBufferCreateInfo
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
            size = vertBytes + idxBytes
        };
        SDL_GPUTransferBuffer* xferBuffer = SDL3.SDL_CreateGPUTransferBuffer(_device, &xferInfo);
        if (xferBuffer == null) return;

        byte* mapPtr = (byte*)SDL3.SDL_MapGPUTransferBuffer(_device, xferBuffer, false);
        if (mapPtr != null)
        {
            fixed (Vertex2D* vPtr = _vertexBuffer)
            {
                Buffer.MemoryCopy(vPtr, mapPtr, vertBytes, vertBytes);
            }
            fixed (ushort* iPtr = _indexBuffer)
            {
                Buffer.MemoryCopy(iPtr, mapPtr + vertBytes, idxBytes, idxBytes);
            }
            SDL3.SDL_UnmapGPUTransferBuffer(_device, xferBuffer);

            SDL_GPUCopyPass* copyPass = SDL3.SDL_BeginGPUCopyPass(cmdBuf);
            if (copyPass != null)
            {
                SDL_GPUTransferBufferLocation vertSrc = new SDL_GPUTransferBufferLocation
                {
                    transfer_buffer = xferBuffer,
                    offset = 0
                };
                SDL_GPUBufferRegion vertDst = new SDL_GPUBufferRegion
                {
                    buffer = _vertexBufferGPU,
                    offset = 0,
                    size = vertBytes
                };
                SDL3.SDL_UploadToGPUBuffer(copyPass, &vertSrc, &vertDst, false);

                SDL_GPUTransferBufferLocation idxSrc = new SDL_GPUTransferBufferLocation
                {
                    transfer_buffer = xferBuffer,
                    offset = vertBytes
                };
                SDL_GPUBufferRegion idxDst = new SDL_GPUBufferRegion
                {
                    buffer = _indexBufferGPU,
                    offset = 0,
                    size = idxBytes
                };
                SDL3.SDL_UploadToGPUBuffer(copyPass, &idxSrc, &idxDst, false);

                SDL3.SDL_EndGPUCopyPass(copyPass);
            }
        }

        SDL3.SDL_ReleaseGPUTransferBuffer(_device, xferBuffer);
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
        if (_pipeline != null)
        {
            SDL3.SDL_ReleaseGPUGraphicsPipeline(_device, _pipeline);
            _pipeline = null;
        }
        if (_vertexBufferGPU != null)
        {
            SDL3.SDL_ReleaseGPUBuffer(_device, _vertexBufferGPU);
            _vertexBufferGPU = null;
        }
        if (_indexBufferGPU != null)
        {
            SDL3.SDL_ReleaseGPUBuffer(_device, _indexBufferGPU);
            _indexBufferGPU = null;
        }
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
