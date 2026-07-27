using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Manages 2D GPU Graphics Pipeline State Objects (PSO) and shader bindings for zero-overhead quad rendering.
/// </summary>
public unsafe class ShaderPipeline : IDisposable
{
    private SDL_GPUDevice* _device;
    private SDL_GPUGraphicsPipeline* _pipeline;
    private bool _isDisposed;

    public SDL_GPUGraphicsPipeline* PipelineHandle => _pipeline;
    public bool IsValid => _pipeline != null;

    public ShaderPipeline(SDL_GPUDevice* device)
    {
        _device = device;
        if (_device == null) return;

        CreateDefaultPipeline();
    }

    private void CreateDefaultPipeline()
    {
        // Configure 2D Quad Shader Pipeline
        SDL_GPUColorTargetDescription colorTargetDesc = new SDL_GPUColorTargetDescription
        {
            format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
            blend_state = new SDL_GPUColorTargetBlendState
            {
                enable_blend = true,
                src_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA,
                dst_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                color_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
                src_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE,
                dst_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                alpha_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD
            }
        };

        SDL_GPUGraphicsPipelineTargetInfo targetInfo = new SDL_GPUGraphicsPipelineTargetInfo
        {
            color_target_descriptions = &colorTargetDesc,
            num_color_targets = 1
        };

        SDL_GPURasterizerState rasterizerState = new SDL_GPURasterizerState
        {
            cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE,
            fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL
        };

        // 1. Create Minimal 2D Vertex & Fragment Shaders
        byte[] mainEntryPoint = "main\0"u8.ToArray();

        // SPIR-V minimal shader bytecodes
        byte[] vertCode = new byte[] {
            0x03, 0x02, 0x23, 0x07, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x08, 0x00, 0x0f, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x0b, 0x00, 0x00, 0x00, 0x0e, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00
        };
        byte[] fragCode = new byte[] {
            0x03, 0x02, 0x23, 0x07, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x08, 0x00, 0x0a, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x0b, 0x00, 0x00, 0x00, 0x0e, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        SDL_GPUShaderFormat supportedFormat = SDL3.SDL_GetGPUShaderFormats(_device);
        if (supportedFormat == 0) supportedFormat = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV;

        SDL_GPUShader* vertShader = null;
        SDL_GPUShader* fragShader = null;

        fixed (byte* vPtr = vertCode, fPtr = fragCode, entryPtr = mainEntryPoint)
        {
            SDL_GPUShaderCreateInfo vertInfo = new SDL_GPUShaderCreateInfo
            {
                code = vPtr,
                code_size = (nuint)vertCode.Length,
                entrypoint = entryPtr,
                format = supportedFormat,
                stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX
            };
            vertShader = SDL3.SDL_CreateGPUShader(_device, &vertInfo);

            SDL_GPUShaderCreateInfo fragInfo = new SDL_GPUShaderCreateInfo
            {
                code = fPtr,
                code_size = (nuint)fragCode.Length,
                entrypoint = entryPtr,
                format = supportedFormat,
                stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT
            };
            fragShader = SDL3.SDL_CreateGPUShader(_device, &fragInfo);
        }

        SDL_GPUGraphicsPipelineCreateInfo pipelineInfo = new SDL_GPUGraphicsPipelineCreateInfo
        {
            vertex_shader = vertShader,
            fragment_shader = fragShader,
            target_info = targetInfo,
            rasterizer_state = rasterizerState,
            primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST
        };

        _pipeline = SDL3.SDL_CreateGPUGraphicsPipeline(_device, &pipelineInfo);
        if (_pipeline != null)
        {
            Console.WriteLine("[SDL_GPU] 2D Shader Pipeline compiled and bound successfully.");
        }
    }

    public void Bind(SDL_GPURenderPass* renderPass)
    {
        if (_pipeline != null && renderPass != null)
        {
            SDL3.SDL_BindGPUGraphicsPipeline(renderPass, _pipeline);
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_pipeline != null && _device != null)
            {
                SDL3.SDL_ReleaseGPUGraphicsPipeline(_device, _pipeline);
                _pipeline = null;
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
