using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Manages 2D GPU Graphics Pipeline State Objects (PSO) for color and textured quad rendering.
/// Shaders compiled via glslangValidator from GLSL source in samples/OpenRei.DemoSDLGPU/Shaders/
/// Command: glslang -V shader.vert -o shader.vert.spv
/// </summary>
public enum PipelineType
{
    ColorQuad,
    TexturedQuad
}

public unsafe class ShaderPipeline : IDisposable
{
    private SDL_GPUDevice* _device;
    private SDL_GPUGraphicsPipeline* _colorPipeline;
    private SDL_GPUGraphicsPipeline* _texturedPipeline;
    private bool _isDisposed;

    public SDL_GPUGraphicsPipeline* ColorHandle => _colorPipeline;
    public SDL_GPUGraphicsPipeline* TexturedHandle => _texturedPipeline;
    public bool IsValid => _colorPipeline != null;

    public ShaderPipeline(SDL_GPUDevice* device)
    {
        _device = device;
    }

    /// <summary>Creates both color and textured pipelines for the given swapchain format.</summary>
    public bool CreatePipelines(SDL_GPUTextureFormat swapchainFormat)
    {
        if (_device == null) return false;
        bool ok = true;
        ok &= CreateColorPipeline(swapchainFormat);
        ok &= CreateTexturedPipeline(swapchainFormat);
        return ok;
    }

    public void Bind(SDL_GPURenderPass* pass, PipelineType type = PipelineType.ColorQuad)
    {
        var pipeline = type == PipelineType.TexturedQuad ? _texturedPipeline : _colorPipeline;
        if (pipeline != null && pass != null)
            SDL3.SDL_BindGPUGraphicsPipeline(pass, pipeline);
    }

    // ── Color quad pipeline: position (vec2) + color (vec4), no texture ──────────
    // Vertex: attrib 0 = float2 position, attrib 1 = float4 color
    private bool CreateColorPipeline(SDL_GPUTextureFormat fmt)
    {
        // SPIR-V compiled from:
        // #version 450
        // layout(location=0) in vec2 a_Position;
        // layout(location=1) in vec4 a_Color;
        // layout(location=0) out vec4 v_Color;
        // void main() { gl_Position = vec4(a_Position, 0, 1); v_Color = a_Color; }
        byte[] vert = new byte[] { 3,2,35,7,0,0,1,0,11,0,8,0,31,0,0,0,0,0,0,0,17,0,2,0,1,0,0,0,11,0,6,0,1,0,0,0,71,76,83,76,46,115,116,100,46,52,53,48,0,0,0,0,14,0,3,0,0,0,0,0,1,0,0,0,15,0,9,0,0,0,0,0,4,0,0,0,109,97,105,110,0,0,0,0,13,0,0,0,18,0,0,0,27,0,0,0,29,0,0,0,3,0,3,0,2,0,0,0,194,1,0,0,5,0,4,0,4,0,0,0,109,97,105,110,0,0,0,0,5,0,6,0,11,0,0,0,103,108,95,80,101,114,86,101,114,116,101,120,0,0,0,0,6,0,6,0,11,0,0,0,0,0,0,0,103,108,95,80,111,115,105,116,105,111,110,0,6,0,7,0,11,0,0,0,1,0,0,0,103,108,95,80,111,105,110,116,83,105,122,101,0,0,0,0,6,0,7,0,11,0,0,0,2,0,0,0,103,108,95,67,108,105,112,68,105,115,116,97,110,99,101,0,6,0,7,0,11,0,0,0,3,0,0,0,103,108,95,67,117,108,108,68,105,115,116,97,110,99,101,0,5,0,3,0,13,0,0,0,0,0,0,0,5,0,5,0,18,0,0,0,97,95,80,111,115,105,116,105,111,110,0,0,5,0,4,0,27,0,0,0,118,95,67,111,108,111,114,0,5,0,4,0,29,0,0,0,97,95,67,111,108,111,114,0,71,0,3,0,11,0,0,0,2,0,0,0,72,0,5,0,11,0,0,0,0,0,0,0,11,0,0,0,0,0,0,0,72,0,5,0,11,0,0,0,1,0,0,0,11,0,0,0,1,0,0,0,72,0,5,0,11,0,0,0,2,0,0,0,11,0,0,0,3,0,0,0,72,0,5,0,11,0,0,0,3,0,0,0,11,0,0,0,4,0,0,0,71,0,4,0,18,0,0,0,30,0,0,0,0,0,0,0,71,0,4,0,27,0,0,0,30,0,0,0,0,0,0,0,71,0,4,0,29,0,0,0,30,0,0,0,1,0,0,0,19,0,2,0,2,0,0,0,33,0,3,0,3,0,0,0,2,0,0,0,22,0,3,0,6,0,0,0,32,0,0,0,23,0,4,0,7,0,0,0,6,0,0,0,4,0,0,0,21,0,4,0,8,0,0,0,32,0,0,0,0,0,0,0,43,0,4,0,8,0,0,0,9,0,0,0,1,0,0,0,28,0,4,0,10,0,0,0,6,0,0,0,9,0,0,0,30,0,6,0,11,0,0,0,7,0,0,0,6,0,0,0,10,0,0,0,10,0,0,0,32,0,4,0,12,0,0,0,3,0,0,0,11,0,0,0,59,0,4,0,12,0,0,0,13,0,0,0,3,0,0,0,21,0,4,0,14,0,0,0,32,0,0,0,1,0,0,0,43,0,4,0,14,0,0,0,15,0,0,0,0,0,0,0,23,0,4,0,16,0,0,0,6,0,0,0,2,0,0,0,32,0,4,0,17,0,0,0,1,0,0,0,16,0,0,0,59,0,4,0,17,0,0,0,18,0,0,0,1,0,0,0,43,0,4,0,6,0,0,0,20,0,0,0,0,0,0,0,43,0,4,0,6,0,0,0,21,0,0,0,0,0,128,63,32,0,4,0,25,0,0,0,3,0,0,0,7,0,0,0,59,0,4,0,25,0,0,0,27,0,0,0,3,0,0,0,32,0,4,0,28,0,0,0,1,0,0,0,7,0,0,0,59,0,4,0,28,0,0,0,29,0,0,0,1,0,0,0,54,0,5,0,2,0,0,0,4,0,0,0,0,0,0,0,3,0,0,0,248,0,2,0,5,0,0,0,61,0,4,0,16,0,0,0,19,0,0,0,18,0,0,0,81,0,5,0,6,0,0,0,22,0,0,0,19,0,0,0,0,0,0,0,81,0,5,0,6,0,0,0,23,0,0,0,19,0,0,0,1,0,0,0,80,0,7,0,7,0,0,0,24,0,0,0,22,0,0,0,23,0,0,0,20,0,0,0,21,0,0,0,65,0,5,0,25,0,0,0,26,0,0,0,13,0,0,0,15,0,0,0,62,0,3,0,26,0,0,0,24,0,0,0,61,0,4,0,7,0,0,0,30,0,0,0,29,0,0,0,62,0,3,0,27,0,0,0,30,0,0,0,253,0,1,0,56,0,1,0 };
        byte[] frag = new byte[] { 3,2,35,7,0,0,1,0,11,0,8,0,13,0,0,0,0,0,0,0,17,0,2,0,1,0,0,0,11,0,6,0,1,0,0,0,71,76,83,76,46,115,116,100,46,52,53,48,0,0,0,0,14,0,3,0,0,0,0,0,1,0,0,0,15,0,7,0,4,0,0,0,4,0,0,0,109,97,105,110,0,0,0,0,9,0,0,0,11,0,0,0,16,0,3,0,4,0,0,0,7,0,0,0,3,0,3,0,2,0,0,0,194,1,0,0,5,0,4,0,4,0,0,0,109,97,105,110,0,0,0,0,5,0,5,0,9,0,0,0,102,114,97,103,67,111,108,111,114,0,0,0,5,0,4,0,11,0,0,0,118,95,67,111,108,111,114,0,71,0,4,0,9,0,0,0,30,0,0,0,0,0,0,0,71,0,4,0,11,0,0,0,30,0,0,0,0,0,0,0,19,0,2,0,2,0,0,0,33,0,3,0,3,0,0,0,2,0,0,0,22,0,3,0,6,0,0,0,32,0,0,0,23,0,4,0,7,0,0,0,6,0,0,0,4,0,0,0,32,0,4,0,8,0,0,0,3,0,0,0,7,0,0,0,59,0,4,0,8,0,0,0,9,0,0,0,3,0,0,0,32,0,4,0,10,0,0,0,1,0,0,0,7,0,0,0,59,0,4,0,10,0,0,0,11,0,0,0,1,0,0,0,54,0,5,0,2,0,0,0,4,0,0,0,0,0,0,0,3,0,0,0,248,0,2,0,5,0,0,0,61,0,4,0,7,0,0,0,12,0,0,0,11,0,0,0,62,0,3,0,9,0,0,0,12,0,0,0,253,0,1,0,56,0,1,0 };

        return CreatePipelineFromShaders(fmt, vert, frag, 0, 0, out _colorPipeline);
    }

    // ── Textured quad pipeline: position (vec2) + texcoord (vec2) + color (vec4) ─
    // Vertex: attrib 0 = float2 position, attrib 1 = float2 texcoord, attrib 2 = float4 color
    // Fragment: 1 combined sampler (set=2, binding=0)
    private bool CreateTexturedPipeline(SDL_GPUTextureFormat fmt)
    {
        byte[] vert = new byte[] { 3,2,35,7,0,0,1,0,11,0,8,0,35,0,0,0,0,0,0,0,17,0,2,0,1,0,0,0,11,0,6,0,1,0,0,0,71,76,83,76,46,115,116,100,46,52,53,48,0,0,0,0,14,0,3,0,0,0,0,0,1,0,0,0,15,0,11,0,0,0,0,0,4,0,0,0,109,97,105,110,0,0,0,0,13,0,0,0,18,0,0,0,28,0,0,0,29,0,0,0,31,0,0,0,33,0,0,0,3,0,3,0,2,0,0,0,194,1,0,0,5,0,4,0,4,0,0,0,109,97,105,110,0,0,0,0,5,0,6,0,11,0,0,0,103,108,95,80,101,114,86,101,114,116,101,120,0,0,0,0,6,0,6,0,11,0,0,0,0,0,0,0,103,108,95,80,111,115,105,116,105,111,110,0,6,0,7,0,11,0,0,0,1,0,0,0,103,108,95,80,111,105,110,116,83,105,122,101,0,0,0,0,6,0,7,0,11,0,0,0,2,0,0,0,103,108,95,67,108,105,112,68,105,115,116,97,110,99,101,0,6,0,7,0,11,0,0,0,3,0,0,0,103,108,95,67,117,108,108,68,105,115,116,97,110,99,101,0,5,0,3,0,13,0,0,0,0,0,0,0,5,0,5,0,18,0,0,0,97,95,80,111,115,105,116,105,111,110,0,0,5,0,5,0,28,0,0,0,118,95,84,101,120,67,111,111,114,100,0,0,5,0,5,0,29,0,0,0,97,95,84,101,120,67,111,111,114,100,0,0,5,0,4,0,31,0,0,0,118,95,67,111,108,111,114,0,5,0,4,0,33,0,0,0,97,95,67,111,108,111,114,0,71,0,3,0,11,0,0,0,2,0,0,0,72,0,5,0,11,0,0,0,0,0,0,0,11,0,0,0,0,0,0,0,72,0,5,0,11,0,0,0,1,0,0,0,11,0,0,0,1,0,0,0,72,0,5,0,11,0,0,0,2,0,0,0,11,0,0,0,3,0,0,0,72,0,5,0,11,0,0,0,3,0,0,0,11,0,0,0,4,0,0,0,71,0,4,0,18,0,0,0,30,0,0,0,0,0,0,0,71,0,4,0,28,0,0,0,30,0,0,0,0,0,0,0,71,0,4,0,29,0,0,0,30,0,0,0,1,0,0,0,71,0,4,0,31,0,0,0,30,0,0,0,1,0,0,0,71,0,4,0,33,0,0,0,30,0,0,0,2,0,0,0,19,0,2,0,2,0,0,0,33,0,3,0,3,0,0,0,2,0,0,0,22,0,3,0,6,0,0,0,32,0,0,0,23,0,4,0,7,0,0,0,6,0,0,0,4,0,0,0,21,0,4,0,8,0,0,0,32,0,0,0,0,0,0,0,43,0,4,0,8,0,0,0,9,0,0,0,1,0,0,0,28,0,4,0,10,0,0,0,6,0,0,0,9,0,0,0,30,0,6,0,11,0,0,0,7,0,0,0,6,0,0,0,10,0,0,0,10,0,0,0,32,0,4,0,12,0,0,0,3,0,0,0,11,0,0,0,59,0,4,0,12,0,0,0,13,0,0,0,3,0,0,0,21,0,4,0,14,0,0,0,32,0,0,0,1,0,0,0,43,0,4,0,14,0,0,0,15,0,0,0,0,0,0,0,23,0,4,0,16,0,0,0,6,0,0,0,2,0,0,0,32,0,4,0,17,0,0,0,1,0,0,0,16,0,0,0,59,0,4,0,17,0,0,0,18,0,0,0,1,0,0,0,43,0,4,0,6,0,0,0,20,0,0,0,0,0,0,0,43,0,4,0,6,0,0,0,21,0,0,0,0,0,128,63,32,0,4,0,25,0,0,0,3,0,0,0,7,0,0,0,32,0,4,0,27,0,0,0,3,0,0,0,16,0,0,0,59,0,4,0,27,0,0,0,28,0,0,0,3,0,0,0,59,0,4,0,17,0,0,0,29,0,0,0,1,0,0,0,59,0,4,0,25,0,0,0,31,0,0,0,3,0,0,0,32,0,4,0,32,0,0,0,1,0,0,0,7,0,0,0,59,0,4,0,32,0,0,0,33,0,0,0,1,0,0,0,54,0,5,0,2,0,0,0,4,0,0,0,0,0,0,0,3,0,0,0,248,0,2,0,5,0,0,0,61,0,4,0,16,0,0,0,19,0,0,0,18,0,0,0,81,0,5,0,6,0,0,0,22,0,0,0,19,0,0,0,0,0,0,0,81,0,5,0,6,0,0,0,23,0,0,0,19,0,0,0,1,0,0,0,80,0,7,0,7,0,0,0,24,0,0,0,22,0,0,0,23,0,0,0,20,0,0,0,21,0,0,0,65,0,5,0,25,0,0,0,26,0,0,0,13,0,0,0,15,0,0,0,62,0,3,0,26,0,0,0,24,0,0,0,61,0,4,0,16,0,0,0,30,0,0,0,29,0,0,0,62,0,3,0,28,0,0,0,30,0,0,0,61,0,4,0,7,0,0,0,34,0,0,0,33,0,0,0,62,0,3,0,31,0,0,0,34,0,0,0,253,0,1,0,56,0,1,0 };
        /* fragment SPIR-V: set=2, binding=0 for SDL_GPU combined sampler */
        byte[] frag = new byte[] { 3,2,35,7,0,0,1,0,11,0,8,0,24,0,0,0,0,0,0,0,17,0,2,0,1,0,0,0,11,0,6,0,1,0,0,0,71,76,83,76,46,115,116,100,46,52,53,48,0,0,0,0,14,0,3,0,0,0,0,0,1,0,0,0,15,0,8,0,4,0,0,0,4,0,0,0,109,97,105,110,0,0,0,0,9,0,0,0,17,0,0,0,21,0,0,0,16,0,3,0,4,0,0,0,7,0,0,0,3,0,3,0,2,0,0,0,194,1,0,0,5,0,4,0,4,0,0,0,109,97,105,110,0,0,0,0,5,0,5,0,9,0,0,0,102,114,97,103,67,111,108,111,114,0,0,0,5,0,5,0,13,0,0,0,117,95,84,101,120,116,117,114,101,0,0,0,5,0,5,0,17,0,0,0,118,95,84,101,120,67,111,111,114,100,0,0,5,0,4,0,21,0,0,0,118,95,67,111,108,111,114,0,71,0,4,0,9,0,0,0,30,0,0,0,0,0,0,0,71,0,4,0,13,0,0,0,33,0,0,0,0,0,0,0,71,0,4,0,13,0,0,0,34,0,0,0,2,0,0,0,71,0,4,0,17,0,0,0,30,0,0,0,0,0,0,0,71,0,4,0,21,0,0,0,30,0,0,0,1,0,0,0,19,0,2,0,2,0,0,0,33,0,3,0,3,0,0,0,2,0,0,0,22,0,3,0,6,0,0,0,32,0,0,0,23,0,4,0,7,0,0,0,6,0,0,0,4,0,0,0,32,0,4,0,8,0,0,0,3,0,0,0,7,0,0,0,59,0,4,0,8,0,0,0,9,0,0,0,3,0,0,0,25,0,9,0,10,0,0,0,6,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,27,0,3,0,11,0,0,0,10,0,0,0,32,0,4,0,12,0,0,0,0,0,0,0,11,0,0,0,59,0,4,0,12,0,0,0,13,0,0,0,0,0,0,0,23,0,4,0,15,0,0,0,6,0,0,0,2,0,0,0,32,0,4,0,16,0,0,0,1,0,0,0,15,0,0,0,59,0,4,0,16,0,0,0,17,0,0,0,1,0,0,0,32,0,4,0,20,0,0,0,1,0,0,0,7,0,0,0,59,0,4,0,20,0,0,0,21,0,0,0,1,0,0,0,54,0,5,0,2,0,0,0,4,0,0,0,0,0,0,0,3,0,0,0,248,0,2,0,5,0,0,0,61,0,4,0,11,0,0,0,14,0,0,0,13,0,0,0,61,0,4,0,15,0,0,0,18,0,0,0,17,0,0,0,87,0,5,0,7,0,0,0,19,0,0,0,14,0,0,0,18,0,0,0,61,0,4,0,7,0,0,0,22,0,0,0,21,0,0,0,133,0,5,0,7,0,0,0,23,0,0,0,19,0,0,0,22,0,0,0,62,0,3,0,9,0,0,0,23,0,0,0,253,0,1,0,56,0,1,0 };

        return CreatePipelineFromShaders(fmt, vert, frag, 0, 1, out _texturedPipeline);
    }

    private bool CreatePipelineFromShaders(
        SDL_GPUTextureFormat fmt,
        byte[] vertCode, byte[] fragCode,
        int numUniformBuffers, int numSamplers,
        out SDL_GPUGraphicsPipeline* pipeline)
    {
        pipeline = null;
        byte[] entry = "main\0"u8.ToArray();

        fixed (byte* v = vertCode, f = fragCode, e = entry)
        {
            var vi = new SDL_GPUShaderCreateInfo
            {
                code = v, code_size = (nuint)vertCode.Length,
                entrypoint = e,
                format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
                stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX,
                num_uniform_buffers = (uint)numUniformBuffers
            };
            var vs = SDL3.SDL_CreateGPUShader(_device, &vi);
            var fi = new SDL_GPUShaderCreateInfo
            {
                code = f, code_size = (nuint)fragCode.Length,
                entrypoint = e,
                format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
                stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT,
                num_samplers = (uint)numSamplers
            };
            var fs = SDL3.SDL_CreateGPUShader(_device, &fi);
            if (vs == null || fs == null)
            {
                Console.WriteLine("[ShaderPipeline] Shader creation failed");
                if (vs != null) SDL3.SDL_ReleaseGPUShader(_device, vs);
                if (fs != null) SDL3.SDL_ReleaseGPUShader(_device, fs);
                return false;
            }

            // Vertex input state varies by pipeline type
            bool textured = numSamplers > 0;
            int attrCount = textured ? 3 : 2;
            uint stride = textured ? 32u : 24u;

            var vbDesc = new SDL_GPUVertexBufferDescription
            {
                slot = 0, pitch = stride,
                input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
                instance_step_rate = 0
            };

            // Array on stack (avoid managed alloc for hot-path setup — this is init code, one-time)
            var attrs = stackalloc SDL_GPUVertexAttribute[3];
            attrs[0] = new SDL_GPUVertexAttribute { location = 0, buffer_slot = 0, format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, offset = 0 };
            attrs[1] = new SDL_GPUVertexAttribute { location = 1, buffer_slot = 0,
                format = textured ? SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2 : SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4,
                offset = textured ? 8u : 8u };
            if (textured)
                attrs[2] = new SDL_GPUVertexAttribute { location = 2, buffer_slot = 0, format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4, offset = 16 };

            var vis = new SDL_GPUVertexInputState
            {
                vertex_buffer_descriptions = &vbDesc,
                num_vertex_buffers = 1,
                vertex_attributes = attrs,
                num_vertex_attributes = (uint)attrCount
            };

            var cd = new SDL_GPUColorTargetDescription
            {
                format = fmt,
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
            var ti = new SDL_GPUGraphicsPipelineTargetInfo { color_target_descriptions = &cd, num_color_targets = 1 };
            var rs = new SDL_GPURasterizerState { cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE, fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL };
            var pi = new SDL_GPUGraphicsPipelineCreateInfo
            {
                vertex_shader = vs, fragment_shader = fs,
                vertex_input_state = vis,
                target_info = ti, rasterizer_state = rs,
                primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST
            };
            pipeline = SDL3.SDL_CreateGPUGraphicsPipeline(_device, &pi);

            SDL3.SDL_ReleaseGPUShader(_device, vs);
            SDL3.SDL_ReleaseGPUShader(_device, fs);
        }

        if (pipeline == null)
            Console.WriteLine($"[ShaderPipeline] Pipeline creation failed: {SDL3.SDL_GetError()}");
        return pipeline != null;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_colorPipeline != null && _device != null)
                SDL3.SDL_ReleaseGPUGraphicsPipeline(_device, _colorPipeline);
            if (_texturedPipeline != null && _device != null)
                SDL3.SDL_ReleaseGPUGraphicsPipeline(_device, _texturedPipeline);
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
