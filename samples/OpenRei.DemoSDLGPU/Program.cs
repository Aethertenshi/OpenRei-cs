using System.Runtime.InteropServices;
using OpenRei.Graphics;
using SDL;

namespace OpenRei.DemoSDLGPU;

internal static unsafe class Program
{
    private static SDL_Window* _window;
    private static SDL_GPUDevice* _device;
    private static SDL_GPUGraphicsPipeline* _colorPipeline;
    private static SDL_GPUGraphicsPipeline* _texturePipeline;
    private static SDL_GPUBuffer* _vertexBuffer;
    private static SDL_GPUTexture* _textTexture;
    private static SDL_GPUSampler* _sampler;
    private static SDL_GPUBuffer* _textVB;
    private static SDL_GPUTextureFormat _swapchainFormat;
    private static bool _running = true;

    private struct Vertex { public float PX, PY; public float CR, CG, CB, CA; }
    private struct TexturedVertex { public float PX, PY; public float TX, TY; public float CR, CG, CB, CA; }

    private static void Main()
    {
        if (!SDL3.SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO)) { Console.WriteLine("[FATAL] SDL3"); return; }

        _window = SDL3.SDL_CreateWindow("OpenRei SDL_GPU Phase 3", 1280, 720,
            SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY | SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
        if (_window == null) { Console.WriteLine("[FATAL] Window"); SDL3.SDL_Quit(); return; }

        var formats = new[] { SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL, SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL };
        foreach (var f in formats) { _device = SDL3.SDL_CreateGPUDevice(f, false, (byte*)null); if (_device != null) break; }
        if (_device == null) _device = SDL3.SDL_CreateGPUDevice(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV | SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL | SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL, false, (byte*)null);
        if (_device == null) { Console.WriteLine("[FATAL] GPU"); goto cleanup; }
        Console.WriteLine("[GPU] Device created.");
        if (!SDL3.SDL_ClaimWindowForGPUDevice(_device, _window)) { Console.WriteLine("[FATAL] Claim"); goto cleanupDevice; }
        SDL3.SDL_SetGPUSwapchainParameters(_device, _window, SDL_GPUSwapchainComposition.SDL_GPU_SWAPCHAINCOMPOSITION_SDR, SDL_GPUPresentMode.SDL_GPU_PRESENTMODE_VSYNC);
        _swapchainFormat = SDL3.SDL_GetGPUSwapchainTextureFormat(_device, _window);
        Console.WriteLine($"[GPU] Swapchain format: {_swapchainFormat}");

        CreateColorQuadPipeline();
        CreateTexturedPipeline();
        CreateTextTexture();

        Console.WriteLine("[GPU] All pipelines + textures ready.");
        Console.WriteLine("[App] Running. ESC or close to exit.");

        _running = true; SDL_Event ev;
        while (_running)
        {
            while (SDL3.SDL_PollEvent(&ev))
            {
                var t = (SDL_EventType)ev.type;
                if (t == SDL_EventType.SDL_EVENT_QUIT) _running = false;
                else if (t == SDL_EventType.SDL_EVENT_KEY_DOWN && ev.key.key == SDL_Keycode.SDLK_ESCAPE) _running = false;
            }

            var cmdBuf = SDL3.SDL_AcquireGPUCommandBuffer(_device);
            if (cmdBuf == null) continue;
            SDL_GPUTexture* swapTex = null;
            if (!SDL3.SDL_AcquireGPUSwapchainTexture(cmdBuf, _window, &swapTex, null, null)) { SDL3.SDL_SubmitGPUCommandBuffer(cmdBuf); continue; }
            if (swapTex == null) { SDL3.SDL_SubmitGPUCommandBuffer(cmdBuf); continue; }

            var ct = new SDL_GPUColorTargetInfo { texture = swapTex, clear_color = new SDL_FColor { r = 0.07f, g = 0.07f, b = 0.12f, a = 1.0f }, load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR, store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE };
            var pass = SDL3.SDL_BeginGPURenderPass(cmdBuf, &ct, 1, null);

            int drawW, drawH;
            SDL3.SDL_GetWindowSizeInPixels(_window, &drawW, &drawH);
            SDL_GPUViewport vp = new SDL_GPUViewport { x = 0, y = 0, w = drawW, h = drawH, min_depth = 0, max_depth = 1 };
            SDL3.SDL_SetGPUViewport(pass, &vp);

            // Draw colored quad
            SDL3.SDL_BindGPUGraphicsPipeline(pass, _colorPipeline);
            var vbBind = new SDL_GPUBufferBinding { buffer = _vertexBuffer, offset = 0 };
            SDL3.SDL_BindGPUVertexBuffers(pass, 0, &vbBind, 1);
            SDL3.SDL_DrawGPUPrimitives(pass, 6, 1, 0, 0);

            // Draw text texture
            if (_texturePipeline != null && _textVB != null)
            {
                SDL3.SDL_BindGPUGraphicsPipeline(pass, _texturePipeline);
                var tvBind = new SDL_GPUBufferBinding { buffer = _textVB, offset = 0 };
                SDL3.SDL_BindGPUVertexBuffers(pass, 0, &tvBind, 1);

                var texBind = new SDL_GPUTextureSamplerBinding { texture = _textTexture, sampler = _sampler };
                SDL3.SDL_BindGPUFragmentSamplers(pass, 0, &texBind, 1);

                SDL3.SDL_DrawGPUPrimitives(pass, 6, 1, 0, 0);
            }

            SDL3.SDL_EndGPURenderPass(pass);
            SDL3.SDL_SubmitGPUCommandBuffer(cmdBuf);
        }

    cleanupDevice:
        if (_texturePipeline != null) SDL3.SDL_ReleaseGPUGraphicsPipeline(_device, _texturePipeline);
        if (_colorPipeline != null) SDL3.SDL_ReleaseGPUGraphicsPipeline(_device, _colorPipeline);
        if (_vertexBuffer != null) SDL3.SDL_ReleaseGPUBuffer(_device, _vertexBuffer);
        if (_textVB != null) SDL3.SDL_ReleaseGPUBuffer(_device, _textVB);
        if (_textTexture != null) SDL3.SDL_ReleaseGPUTexture(_device, _textTexture);
        if (_sampler != null) SDL3.SDL_ReleaseGPUSampler(_device, _sampler);
        SDL3.SDL_ReleaseWindowFromGPUDevice(_device, _window);
        SDL3.SDL_DestroyGPUDevice(_device);
    cleanup:
        SDL3.SDL_DestroyWindow(_window);
        SDL3.SDL_Quit();
        Console.WriteLine("[App] Shutdown.");
    }

    private static void CreateColorQuadPipeline()
    {
        byte[] vertCode = new byte[] { 3, 2, 35, 7, 0, 0, 1, 0, 11, 0, 8, 0, 31, 0, 0, 0, 0, 0, 0, 0, 17, 0, 2, 0, 1, 0, 0, 0, 11, 0, 6, 0, 1, 0, 0, 0, 71, 76, 83, 76, 46, 115, 116, 100, 46, 52, 53, 48, 0, 0, 0, 0, 14, 0, 3, 0, 0, 0, 0, 0, 1, 0, 0, 0, 15, 0, 9, 0, 0, 0, 0, 0, 4, 0, 0, 0, 109, 97, 105, 110, 0, 0, 0, 0, 13, 0, 0, 0, 18, 0, 0, 0, 27, 0, 0, 0, 29, 0, 0, 0, 3, 0, 3, 0, 2, 0, 0, 0, 194, 1, 0, 0, 5, 0, 4, 0, 4, 0, 0, 0, 109, 97, 105, 110, 0, 0, 0, 0, 5, 0, 6, 0, 11, 0, 0, 0, 103, 108, 95, 80, 101, 114, 86, 101, 114, 116, 101, 120, 0, 0, 0, 0, 6, 0, 6, 0, 11, 0, 0, 0, 0, 0, 0, 0, 103, 108, 95, 80, 111, 115, 105, 116, 105, 111, 110, 0, 6, 0, 7, 0, 11, 0, 0, 0, 1, 0, 0, 0, 103, 108, 95, 80, 111, 105, 110, 116, 83, 105, 122, 101, 0, 0, 0, 0, 6, 0, 7, 0, 11, 0, 0, 0, 2, 0, 0, 0, 103, 108, 95, 67, 108, 105, 112, 68, 105, 115, 116, 97, 110, 99, 101, 0, 6, 0, 7, 0, 11, 0, 0, 0, 3, 0, 0, 0, 103, 108, 95, 67, 117, 108, 108, 68, 105, 115, 116, 97, 110, 99, 101, 0, 5, 0, 3, 0, 13, 0, 0, 0, 0, 0, 0, 0, 5, 0, 5, 0, 18, 0, 0, 0, 97, 95, 80, 111, 115, 105, 116, 105, 111, 110, 0, 0, 5, 0, 4, 0, 27, 0, 0, 0, 118, 95, 67, 111, 108, 111, 114, 0, 5, 0, 4, 0, 29, 0, 0, 0, 97, 95, 67, 111, 108, 111, 114, 0, 71, 0, 3, 0, 11, 0, 0, 0, 2, 0, 0, 0, 72, 0, 5, 0, 11, 0, 0, 0, 0, 0, 0, 0, 11, 0, 0, 0, 0, 0, 0, 0, 72, 0, 5, 0, 11, 0, 0, 0, 1, 0, 0, 0, 11, 0, 0, 0, 1, 0, 0, 0, 72, 0, 5, 0, 11, 0, 0, 0, 2, 0, 0, 0, 11, 0, 0, 0, 3, 0, 0, 0, 72, 0, 5, 0, 11, 0, 0, 0, 3, 0, 0, 0, 11, 0, 0, 0, 4, 0, 0, 0, 71, 0, 4, 0, 18, 0, 0, 0, 30, 0, 0, 0, 0, 0, 0, 0, 71, 0, 4, 0, 27, 0, 0, 0, 30, 0, 0, 0, 0, 0, 0, 0, 71, 0, 4, 0, 29, 0, 0, 0, 30, 0, 0, 0, 1, 0, 0, 0, 19, 0, 2, 0, 2, 0, 0, 0, 33, 0, 3, 0, 3, 0, 0, 0, 2, 0, 0, 0, 22, 0, 3, 0, 6, 0, 0, 0, 32, 0, 0, 0, 23, 0, 4, 0, 7, 0, 0, 0, 6, 0, 0, 0, 4, 0, 0, 0, 21, 0, 4, 0, 8, 0, 0, 0, 32, 0, 0, 0, 0, 0, 0, 0, 43, 0, 4, 0, 8, 0, 0, 0, 9, 0, 0, 0, 1, 0, 0, 0, 28, 0, 4, 0, 10, 0, 0, 0, 6, 0, 0, 0, 9, 0, 0, 0, 30, 0, 6, 0, 11, 0, 0, 0, 7, 0, 0, 0, 6, 0, 0, 0, 10, 0, 0, 0, 10, 0, 0, 0, 32, 0, 4, 0, 12, 0, 0, 0, 3, 0, 0, 0, 11, 0, 0, 0, 59, 0, 4, 0, 12, 0, 0, 0, 13, 0, 0, 0, 3, 0, 0, 0, 21, 0, 4, 0, 14, 0, 0, 0, 32, 0, 0, 0, 1, 0, 0, 0, 43, 0, 4, 0, 14, 0, 0, 0, 15, 0, 0, 0, 0, 0, 0, 0, 23, 0, 4, 0, 16, 0, 0, 0, 6, 0, 0, 0, 2, 0, 0, 0, 32, 0, 4, 0, 17, 0, 0, 0, 1, 0, 0, 0, 16, 0, 0, 0, 59, 0, 4, 0, 17, 0, 0, 0, 18, 0, 0, 0, 1, 0, 0, 0, 43, 0, 4, 0, 6, 0, 0, 0, 20, 0, 0, 0, 0, 0, 0, 0, 43, 0, 4, 0, 6, 0, 0, 0, 21, 0, 0, 0, 0, 0, 128, 63, 32, 0, 4, 0, 25, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0, 59, 0, 4, 0, 25, 0, 0, 0, 27, 0, 0, 0, 3, 0, 0, 0, 32, 0, 4, 0, 28, 0, 0, 0, 1, 0, 0, 0, 7, 0, 0, 0, 59, 0, 4, 0, 28, 0, 0, 0, 29, 0, 0, 0, 1, 0, 0, 0, 54, 0, 5, 0, 2, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 248, 0, 2, 0, 5, 0, 0, 0, 61, 0, 4, 0, 16, 0, 0, 0, 19, 0, 0, 0, 18, 0, 0, 0, 81, 0, 5, 0, 6, 0, 0, 0, 22, 0, 0, 0, 19, 0, 0, 0, 0, 0, 0, 0, 81, 0, 5, 0, 6, 0, 0, 0, 23, 0, 0, 0, 19, 0, 0, 0, 1, 0, 0, 0, 80, 0, 7, 0, 7, 0, 0, 0, 24, 0, 0, 0, 22, 0, 0, 0, 23, 0, 0, 0, 20, 0, 0, 0, 21, 0, 0, 0, 65, 0, 5, 0, 25, 0, 0, 0, 26, 0, 0, 0, 13, 0, 0, 0, 15, 0, 0, 0, 62, 0, 3, 0, 26, 0, 0, 0, 24, 0, 0, 0, 61, 0, 4, 0, 7, 0, 0, 0, 30, 0, 0, 0, 29, 0, 0, 0, 62, 0, 3, 0, 27, 0, 0, 0, 30, 0, 0, 0, 253, 0, 1, 0, 56, 0, 1, 0 };
        byte[] fragCode = new byte[] { 3, 2, 35, 7, 0, 0, 1, 0, 11, 0, 8, 0, 13, 0, 0, 0, 0, 0, 0, 0, 17, 0, 2, 0, 1, 0, 0, 0, 11, 0, 6, 0, 1, 0, 0, 0, 71, 76, 83, 76, 46, 115, 116, 100, 46, 52, 53, 48, 0, 0, 0, 0, 14, 0, 3, 0, 0, 0, 0, 0, 1, 0, 0, 0, 15, 0, 7, 0, 4, 0, 0, 0, 4, 0, 0, 0, 109, 97, 105, 110, 0, 0, 0, 0, 9, 0, 0, 0, 11, 0, 0, 0, 16, 0, 3, 0, 4, 0, 0, 0, 7, 0, 0, 0, 3, 0, 3, 0, 2, 0, 0, 0, 194, 1, 0, 0, 5, 0, 4, 0, 4, 0, 0, 0, 109, 97, 105, 110, 0, 0, 0, 0, 5, 0, 5, 0, 9, 0, 0, 0, 102, 114, 97, 103, 67, 111, 108, 111, 114, 0, 0, 0, 5, 0, 4, 0, 11, 0, 0, 0, 118, 95, 67, 111, 108, 111, 114, 0, 71, 0, 4, 0, 9, 0, 0, 0, 30, 0, 0, 0, 0, 0, 0, 0, 71, 0, 4, 0, 11, 0, 0, 0, 30, 0, 0, 0, 0, 0, 0, 0, 19, 0, 2, 0, 2, 0, 0, 0, 33, 0, 3, 0, 3, 0, 0, 0, 2, 0, 0, 0, 22, 0, 3, 0, 6, 0, 0, 0, 32, 0, 0, 0, 23, 0, 4, 0, 7, 0, 0, 0, 6, 0, 0, 0, 4, 0, 0, 0, 32, 0, 4, 0, 8, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0, 59, 0, 4, 0, 8, 0, 0, 0, 9, 0, 0, 0, 3, 0, 0, 0, 32, 0, 4, 0, 10, 0, 0, 0, 1, 0, 0, 0, 7, 0, 0, 0, 59, 0, 4, 0, 10, 0, 0, 0, 11, 0, 0, 0, 1, 0, 0, 0, 54, 0, 5, 0, 2, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 248, 0, 2, 0, 5, 0, 0, 0, 61, 0, 4, 0, 7, 0, 0, 0, 12, 0, 0, 0, 11, 0, 0, 0, 62, 0, 3, 0, 9, 0, 0, 0, 12, 0, 0, 0, 253, 0, 1, 0, 56, 0, 1, 0 };

        byte[] entry = "main\0"u8.ToArray();
        fixed (byte* v = vertCode, f = fragCode, e = entry)
        {
            var vi = new SDL_GPUShaderCreateInfo { code = v, code_size = (nuint)vertCode.Length, entrypoint = e, format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX };
            var vs = SDL3.SDL_CreateGPUShader(_device, &vi);
            var fi = new SDL_GPUShaderCreateInfo { code = f, code_size = (nuint)fragCode.Length, entrypoint = e, format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT };
            var fs = SDL3.SDL_CreateGPUShader(_device, &fi);
            if (vs == null || fs == null) { Console.WriteLine("[FATAL] Color shaders"); return; }

            var vbDesc = new SDL_GPUVertexBufferDescription { slot = 0, pitch = (uint)sizeof(Vertex), input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX, instance_step_rate = 0 };
            var attrs = new SDL_GPUVertexAttribute[] { new SDL_GPUVertexAttribute { location = 0, buffer_slot = 0, format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, offset = 0 }, new SDL_GPUVertexAttribute { location = 1, buffer_slot = 0, format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4, offset = 8 } };
            fixed (SDL_GPUVertexAttribute* a = attrs)
            {
                var vis = new SDL_GPUVertexInputState { vertex_buffer_descriptions = &vbDesc, num_vertex_buffers = 1, vertex_attributes = a, num_vertex_attributes = 2 };
                var cd = new SDL_GPUColorTargetDescription { format = _swapchainFormat, blend_state = new SDL_GPUColorTargetBlendState { enable_blend = true, src_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA, dst_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA, color_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD, src_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE, dst_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA, alpha_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD } };
                var ti = new SDL_GPUGraphicsPipelineTargetInfo { color_target_descriptions = &cd, num_color_targets = 1 };
                var rs = new SDL_GPURasterizerState { cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE, fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL };
                var pi = new SDL_GPUGraphicsPipelineCreateInfo { vertex_shader = vs, fragment_shader = fs, vertex_input_state = vis, target_info = ti, rasterizer_state = rs, primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST };
                _colorPipeline = SDL3.SDL_CreateGPUGraphicsPipeline(_device, &pi);
            }
            if (vs != null) SDL3.SDL_ReleaseGPUShader(_device, vs);
            if (fs != null) SDL3.SDL_ReleaseGPUShader(_device, fs);
        }

        // Color quad vertex buffer (6 vertices = 2 triangles for TRIANGLELIST)
        float s = 0.35f;
        Vertex[] quads = new[] {
            new Vertex { PX = -s, PY = -s, CR = 1, CG = 0, CB = 0, CA = 1 },
            new Vertex { PX =  s, PY = -s, CR = 0, CG = 1, CB = 0, CA = 1 },
            new Vertex { PX = -s, PY =  s, CR = 1, CG = 1, CB = 0, CA = 1 },  // tri 1
            new Vertex { PX =  s, PY = -s, CR = 0, CG = 1, CB = 0, CA = 1 },
            new Vertex { PX =  s, PY =  s, CR = 0, CG = 0, CB = 1, CA = 1 },
            new Vertex { PX = -s, PY =  s, CR = 1, CG = 1, CB = 0, CA = 1 },  // tri 2
        };
        uint vbSize = (uint)(sizeof(Vertex) * quads.Length);
        var si = new SDL_GPUTransferBufferCreateInfo { size = vbSize, usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD };
        var stg = SDL3.SDL_CreateGPUTransferBuffer(_device, &si);
        void* data = (void*)SDL3.SDL_MapGPUTransferBuffer(_device, stg, false);
        byte[] bytes = new byte[vbSize]; fixed (Vertex* src = quads) Marshal.Copy((nint)src, bytes, 0, bytes.Length); Marshal.Copy(bytes, 0, (nint)data, bytes.Length);
        SDL3.SDL_UnmapGPUTransferBuffer(_device, stg);
        var bi = new SDL_GPUBufferCreateInfo { size = vbSize, usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX };
        _vertexBuffer = SDL3.SDL_CreateGPUBuffer(_device, &bi);
        var ucmd = SDL3.SDL_AcquireGPUCommandBuffer(_device);
        var cp = SDL3.SDL_BeginGPUCopyPass(ucmd);
        var sl = new SDL_GPUTransferBufferLocation { transfer_buffer = stg, offset = 0 };
        var dr = new SDL_GPUBufferRegion { buffer = _vertexBuffer, offset = 0, size = vbSize };
        SDL3.SDL_UploadToGPUBuffer(cp, &sl, &dr, false); SDL3.SDL_EndGPUCopyPass(cp);
        SDL3.SDL_SubmitGPUCommandBuffer(ucmd); SDL3.SDL_ReleaseGPUTransferBuffer(_device, stg);
        Console.WriteLine("[GPU] Color pipeline + vertex buffer ready.");
    }

    private static void CreateTexturedPipeline()
    {
        byte[] vertCode = new byte[] { 3, 2, 35, 7, 0, 0, 1, 0, 11, 0, 8, 0, 35, 0, 0, 0, 0, 0, 0, 0, 17, 0, 2, 0, 1, 0, 0, 0, 11, 0, 6, 0, 1, 0, 0, 0, 71, 76, 83, 76, 46, 115, 116, 100, 46, 52, 53, 48, 0, 0, 0, 0, 14, 0, 3, 0, 0, 0, 0, 0, 1, 0, 0, 0, 15, 0, 11, 0, 0, 0, 0, 0, 4, 0, 0, 0, 109, 97, 105, 110, 0, 0, 0, 0, 13, 0, 0, 0, 18, 0, 0, 0, 28, 0, 0, 0, 29, 0, 0, 0, 31, 0, 0, 0, 33, 0, 0, 0, 3, 0, 3, 0, 2, 0, 0, 0, 194, 1, 0, 0, 5, 0, 4, 0, 4, 0, 0, 0, 109, 97, 105, 110, 0, 0, 0, 0, 5, 0, 6, 0, 11, 0, 0, 0, 103, 108, 95, 80, 101, 114, 86, 101, 114, 116, 101, 120, 0, 0, 0, 0, 6, 0, 6, 0, 11, 0, 0, 0, 0, 0, 0, 0, 103, 108, 95, 80, 111, 115, 105, 116, 105, 111, 110, 0, 6, 0, 7, 0, 11, 0, 0, 0, 1, 0, 0, 0, 103, 108, 95, 80, 111, 105, 110, 116, 83, 105, 122, 101, 0, 0, 0, 0, 6, 0, 7, 0, 11, 0, 0, 0, 2, 0, 0, 0, 103, 108, 95, 67, 108, 105, 112, 68, 105, 115, 116, 97, 110, 99, 101, 0, 6, 0, 7, 0, 11, 0, 0, 0, 3, 0, 0, 0, 103, 108, 95, 67, 117, 108, 108, 68, 105, 115, 116, 97, 110, 99, 101, 0, 5, 0, 3, 0, 13, 0, 0, 0, 0, 0, 0, 0, 5, 0, 5, 0, 18, 0, 0, 0, 97, 95, 80, 111, 115, 105, 116, 105, 111, 110, 0, 0, 5, 0, 5, 0, 28, 0, 0, 0, 118, 95, 84, 101, 120, 67, 111, 111, 114, 100, 0, 0, 5, 0, 5, 0, 29, 0, 0, 0, 97, 95, 84, 101, 120, 67, 111, 111, 114, 100, 0, 0, 5, 0, 4, 0, 31, 0, 0, 0, 118, 95, 67, 111, 108, 111, 114, 0, 5, 0, 4, 0, 33, 0, 0, 0, 97, 95, 67, 111, 108, 111, 114, 0, 71, 0, 3, 0, 11, 0, 0, 0, 2, 0, 0, 0, 72, 0, 5, 0, 11, 0, 0, 0, 0, 0, 0, 0, 11, 0, 0, 0, 0, 0, 0, 0, 72, 0, 5, 0, 11, 0, 0, 0, 1, 0, 0, 0, 11, 0, 0, 0, 1, 0, 0, 0, 72, 0, 5, 0, 11, 0, 0, 0, 2, 0, 0, 0, 11, 0, 0, 0, 3, 0, 0, 0, 72, 0, 5, 0, 11, 0, 0, 0, 3, 0, 0, 0, 11, 0, 0, 0, 4, 0, 0, 0, 71, 0, 4, 0, 18, 0, 0, 0, 30, 0, 0, 0, 0, 0, 0, 0, 71, 0, 4, 0, 28, 0, 0, 0, 30, 0, 0, 0, 0, 0, 0, 0, 71, 0, 4, 0, 29, 0, 0, 0, 30, 0, 0, 0, 1, 0, 0, 0, 71, 0, 4, 0, 31, 0, 0, 0, 30, 0, 0, 0, 1, 0, 0, 0, 71, 0, 4, 0, 33, 0, 0, 0, 30, 0, 0, 0, 2, 0, 0, 0, 19, 0, 2, 0, 2, 0, 0, 0, 33, 0, 3, 0, 3, 0, 0, 0, 2, 0, 0, 0, 22, 0, 3, 0, 6, 0, 0, 0, 32, 0, 0, 0, 23, 0, 4, 0, 7, 0, 0, 0, 6, 0, 0, 0, 4, 0, 0, 0, 21, 0, 4, 0, 8, 0, 0, 0, 32, 0, 0, 0, 0, 0, 0, 0, 43, 0, 4, 0, 8, 0, 0, 0, 9, 0, 0, 0, 1, 0, 0, 0, 28, 0, 4, 0, 10, 0, 0, 0, 6, 0, 0, 0, 9, 0, 0, 0, 30, 0, 6, 0, 11, 0, 0, 0, 7, 0, 0, 0, 6, 0, 0, 0, 10, 0, 0, 0, 10, 0, 0, 0, 32, 0, 4, 0, 12, 0, 0, 0, 3, 0, 0, 0, 11, 0, 0, 0, 59, 0, 4, 0, 12, 0, 0, 0, 13, 0, 0, 0, 3, 0, 0, 0, 21, 0, 4, 0, 14, 0, 0, 0, 32, 0, 0, 0, 1, 0, 0, 0, 43, 0, 4, 0, 14, 0, 0, 0, 15, 0, 0, 0, 0, 0, 0, 0, 23, 0, 4, 0, 16, 0, 0, 0, 6, 0, 0, 0, 2, 0, 0, 0, 32, 0, 4, 0, 17, 0, 0, 0, 1, 0, 0, 0, 16, 0, 0, 0, 59, 0, 4, 0, 17, 0, 0, 0, 18, 0, 0, 0, 1, 0, 0, 0, 43, 0, 4, 0, 6, 0, 0, 0, 20, 0, 0, 0, 0, 0, 0, 0, 43, 0, 4, 0, 6, 0, 0, 0, 21, 0, 0, 0, 0, 0, 128, 63, 32, 0, 4, 0, 25, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0, 32, 0, 4, 0, 27, 0, 0, 0, 3, 0, 0, 0, 16, 0, 0, 0, 59, 0, 4, 0, 27, 0, 0, 0, 28, 0, 0, 0, 3, 0, 0, 0, 59, 0, 4, 0, 17, 0, 0, 0, 29, 0, 0, 0, 1, 0, 0, 0, 59, 0, 4, 0, 25, 0, 0, 0, 31, 0, 0, 0, 3, 0, 0, 0, 32, 0, 4, 0, 32, 0, 0, 0, 1, 0, 0, 0, 7, 0, 0, 0, 59, 0, 4, 0, 32, 0, 0, 0, 33, 0, 0, 0, 1, 0, 0, 0, 54, 0, 5, 0, 2, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 248, 0, 2, 0, 5, 0, 0, 0, 61, 0, 4, 0, 16, 0, 0, 0, 19, 0, 0, 0, 18, 0, 0, 0, 81, 0, 5, 0, 6, 0, 0, 0, 22, 0, 0, 0, 19, 0, 0, 0, 0, 0, 0, 0, 81, 0, 5, 0, 6, 0, 0, 0, 23, 0, 0, 0, 19, 0, 0, 0, 1, 0, 0, 0, 80, 0, 7, 0, 7, 0, 0, 0, 24, 0, 0, 0, 22, 0, 0, 0, 23, 0, 0, 0, 20, 0, 0, 0, 21, 0, 0, 0, 65, 0, 5, 0, 25, 0, 0, 0, 26, 0, 0, 0, 13, 0, 0, 0, 15, 0, 0, 0, 62, 0, 3, 0, 26, 0, 0, 0, 24, 0, 0, 0, 61, 0, 4, 0, 16, 0, 0, 0, 30, 0, 0, 0, 29, 0, 0, 0, 62, 0, 3, 0, 28, 0, 0, 0, 30, 0, 0, 0, 61, 0, 4, 0, 7, 0, 0, 0, 34, 0, 0, 0, 33, 0, 0, 0, 62, 0, 3, 0, 31, 0, 0, 0, 34, 0, 0, 0, 253, 0, 1, 0, 56, 0, 1, 0 };
        /* fragment SPIR-V: set=2, binding=0 for SDL_GPU sampler */
        byte[] fragCode = new byte[] { 3, 2, 35, 7, 0, 0, 1, 0, 11, 0, 8, 0, 24, 0, 0, 0, 0, 0, 0, 0, 17, 0, 2, 0, 1, 0, 0, 0, 11, 0, 6, 0, 1, 0, 0, 0, 71, 76, 83, 76, 46, 115, 116, 100, 46, 52, 53, 48, 0, 0, 0, 0, 14, 0, 3, 0, 0, 0, 0, 0, 1, 0, 0, 0, 15, 0, 8, 0, 4, 0, 0, 0, 4, 0, 0, 0, 109, 97, 105, 110, 0, 0, 0, 0, 9, 0, 0, 0, 17, 0, 0, 0, 21, 0, 0, 0, 16, 0, 3, 0, 4, 0, 0, 0, 7, 0, 0, 0, 3, 0, 3, 0, 2, 0, 0, 0, 194, 1, 0, 0, 5, 0, 4, 0, 4, 0, 0, 0, 109, 97, 105, 110, 0, 0, 0, 0, 5, 0, 5, 0, 9, 0, 0, 0, 102, 114, 97, 103, 67, 111, 108, 111, 114, 0, 0, 0, 5, 0, 5, 0, 13, 0, 0, 0, 117, 95, 84, 101, 120, 116, 117, 114, 101, 0, 0, 0, 5, 0, 5, 0, 17, 0, 0, 0, 118, 95, 84, 101, 120, 67, 111, 111, 114, 100, 0, 0, 5, 0, 4, 0, 21, 0, 0, 0, 118, 95, 67, 111, 108, 111, 114, 0, 71, 0, 4, 0, 9, 0, 0, 0, 30, 0, 0, 0, 0, 0, 0, 0, 71, 0, 4, 0, 13, 0, 0, 0, 33, 0, 0, 0, 0, 0, 0, 0, 71, 0, 4, 0, 13, 0, 0, 0, 34, 0, 0, 0, 2, 0, 0, 0, 71, 0, 4, 0, 17, 0, 0, 0, 30, 0, 0, 0, 0, 0, 0, 0, 71, 0, 4, 0, 21, 0, 0, 0, 30, 0, 0, 0, 1, 0, 0, 0, 19, 0, 2, 0, 2, 0, 0, 0, 33, 0, 3, 0, 3, 0, 0, 0, 2, 0, 0, 0, 22, 0, 3, 0, 6, 0, 0, 0, 32, 0, 0, 0, 23, 0, 4, 0, 7, 0, 0, 0, 6, 0, 0, 0, 4, 0, 0, 0, 32, 0, 4, 0, 8, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0, 59, 0, 4, 0, 8, 0, 0, 0, 9, 0, 0, 0, 3, 0, 0, 0, 25, 0, 9, 0, 10, 0, 0, 0, 6, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 27, 0, 3, 0, 11, 0, 0, 0, 10, 0, 0, 0, 32, 0, 4, 0, 12, 0, 0, 0, 0, 0, 0, 0, 11, 0, 0, 0, 59, 0, 4, 0, 12, 0, 0, 0, 13, 0, 0, 0, 0, 0, 0, 0, 23, 0, 4, 0, 15, 0, 0, 0, 6, 0, 0, 0, 2, 0, 0, 0, 32, 0, 4, 0, 16, 0, 0, 0, 1, 0, 0, 0, 15, 0, 0, 0, 59, 0, 4, 0, 16, 0, 0, 0, 17, 0, 0, 0, 1, 0, 0, 0, 32, 0, 4, 0, 20, 0, 0, 0, 1, 0, 0, 0, 7, 0, 0, 0, 59, 0, 4, 0, 20, 0, 0, 0, 21, 0, 0, 0, 1, 0, 0, 0, 54, 0, 5, 0, 2, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 248, 0, 2, 0, 5, 0, 0, 0, 61, 0, 4, 0, 11, 0, 0, 0, 14, 0, 0, 0, 13, 0, 0, 0, 61, 0, 4, 0, 15, 0, 0, 0, 18, 0, 0, 0, 17, 0, 0, 0, 87, 0, 5, 0, 7, 0, 0, 0, 19, 0, 0, 0, 14, 0, 0, 0, 18, 0, 0, 0, 61, 0, 4, 0, 7, 0, 0, 0, 22, 0, 0, 0, 21, 0, 0, 0, 133, 0, 5, 0, 7, 0, 0, 0, 23, 0, 0, 0, 19, 0, 0, 0, 22, 0, 0, 0, 62, 0, 3, 0, 9, 0, 0, 0, 23, 0, 0, 0, 253, 0, 1, 0, 56, 0, 1, 0 };

        byte[] entry = "main\0"u8.ToArray();
        fixed (byte* v = vertCode, f = fragCode, e = entry)
        {
            var vi = new SDL_GPUShaderCreateInfo { code = v, code_size = (nuint)vertCode.Length, entrypoint = e, format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX };
            var vs = SDL3.SDL_CreateGPUShader(_device, &vi);
            var fi = new SDL_GPUShaderCreateInfo { code = f, code_size = (nuint)fragCode.Length, entrypoint = e, format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, num_samplers = 1 };
            var fs = SDL3.SDL_CreateGPUShader(_device, &fi);
            if (vs == null || fs == null) { Console.WriteLine("[FATAL] Tex shaders"); return; }

            var vbDesc = new SDL_GPUVertexBufferDescription { slot = 0, pitch = (uint)sizeof(TexturedVertex), input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX, instance_step_rate = 0 };
            var attrs = new SDL_GPUVertexAttribute[] {
                new SDL_GPUVertexAttribute { location = 0, buffer_slot = 0, format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, offset = 0 },
                new SDL_GPUVertexAttribute { location = 1, buffer_slot = 0, format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, offset = 8 },
                new SDL_GPUVertexAttribute { location = 2, buffer_slot = 0, format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4, offset = 16 }
            };
            fixed (SDL_GPUVertexAttribute* a = attrs)
            {
                var vis = new SDL_GPUVertexInputState { vertex_buffer_descriptions = &vbDesc, num_vertex_buffers = 1, vertex_attributes = a, num_vertex_attributes = 3 };
                var cd = new SDL_GPUColorTargetDescription { format = _swapchainFormat, blend_state = new SDL_GPUColorTargetBlendState { enable_blend = true, src_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA, dst_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA, color_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD, src_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE, dst_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA, alpha_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD } };
                var ti = new SDL_GPUGraphicsPipelineTargetInfo { color_target_descriptions = &cd, num_color_targets = 1 };
                var rs = new SDL_GPURasterizerState { cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE, fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL };
                var pi = new SDL_GPUGraphicsPipelineCreateInfo { vertex_shader = vs, fragment_shader = fs, vertex_input_state = vis, target_info = ti, rasterizer_state = rs, primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST };
                _texturePipeline = SDL3.SDL_CreateGPUGraphicsPipeline(_device, &pi);
                if (_texturePipeline == null)
                    Console.WriteLine($"[WARN] Texture pipeline failed: {SDL3.SDL_GetError()}");
            }
            if (vs != null) SDL3.SDL_ReleaseGPUShader(_device, vs);
            if (fs != null) SDL3.SDL_ReleaseGPUShader(_device, fs);
        }
    }

    private static void CreateTextTexture()
    {
        FontEngine.Initialize();
        // Load font at a large point size so we don't need to scale up
        Font? font = null;
        string[] fontPaths = new[] {
            "GoogleSans-Regular.ttf",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoogleSans-Regular.ttf"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenRei", "GoogleSans-Regular.ttf"),
            Path.Combine("..", "..", "OpenRei", "GoogleSans-Regular.ttf"),
        };
        foreach (var fp in fontPaths)
        {
            if (File.Exists(fp)) { font = new Font(fp, 72f); break; }
        }
        if (font?.Handle == null) { Console.WriteLine("[WARN] No font at 72pt, using default"); font = FontEngine.DefaultFont; }
        if (font?.Handle == null) { Console.WriteLine("[WARN] No font available"); return; }

        string text = "Hello, SDL_GPU!";
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(text + "\0");
        var color = new SDL_Color { r = 255, g = 255, b = 255, a = 255 };

        SDL_Surface* surface;
        fixed (byte* t = utf8)
            surface = SDL3_ttf.TTF_RenderText_Blended(font.Handle, t, (nuint)(utf8.Length - 1), color);
        if (surface == null) { Console.WriteLine($"[WARN] TTF: {SDL3.SDL_GetError()}"); return; }

        // Force convert to RGBA8888 to guarantee pixel layout
        SDL_Surface* converted = SDL3.SDL_ConvertSurface(surface, SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888);
        SDL3.SDL_DestroySurface(surface);
        if (converted == null) { Console.WriteLine($"[WARN] Surface convert: {SDL3.SDL_GetError()}"); return; }
        surface = converted;

        int texW = surface->w, texH = surface->h;
        Console.WriteLine($"[TEXT] '{text}' ({texW}x{texH})");

        var texInfo = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
            width = (uint)texW, height = (uint)texH, layer_count_or_depth = 1, num_levels = 1,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER
        };
        _textTexture = SDL3.SDL_CreateGPUTexture(_device, &texInfo);
        if (_textTexture == null) { SDL3.SDL_DestroySurface(surface); Console.WriteLine("[WARN] Tex"); return; }

        uint bpp = 4;
        uint srcPitch = (uint)surface->pitch;
        uint tightPitch = (uint)texW * bpp;
        uint dataSize = tightPitch * (uint)texH;

        var stgInfo = new SDL_GPUTransferBufferCreateInfo { size = dataSize, usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD };
        var stg = SDL3.SDL_CreateGPUTransferBuffer(_device, &stgInfo);
        byte* mapped = (byte*)SDL3.SDL_MapGPUTransferBuffer(_device, stg, false);
        byte* srcRow = (byte*)surface->pixels;

        for (int y = 0; y < texH; y++)
        {
            Buffer.MemoryCopy(srcRow, mapped + (y * tightPitch), tightPitch, tightPitch);
            srcRow += srcPitch;
        }

        SDL3.SDL_UnmapGPUTransferBuffer(_device, stg);
        SDL3.SDL_DestroySurface(surface);

        var ucmd = SDL3.SDL_AcquireGPUCommandBuffer(_device);
        var cp = SDL3.SDL_BeginGPUCopyPass(ucmd);
        var srcLoc = new SDL_GPUTextureTransferInfo
        {
            transfer_buffer = stg, offset = 0,
            pixels_per_row = (uint)texW,
            rows_per_layer = (uint)texH
        };
        var dstRegion = new SDL_GPUTextureRegion { texture = _textTexture, w = (uint)texW, h = (uint)texH, d = 1 };
        SDL3.SDL_UploadToGPUTexture(cp, &srcLoc, &dstRegion, false);
        SDL3.SDL_EndGPUCopyPass(cp);
        SDL3.SDL_SubmitGPUCommandBuffer(ucmd);
        SDL3.SDL_ReleaseGPUTransferBuffer(_device, stg);

        var sampInfo = new SDL_GPUSamplerCreateInfo
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE
        };
        _sampler = SDL3.SDL_CreateGPUSampler(_device, &sampInfo);

        // Texel-exact sizing: quad covers exactly texW x texH pixels in NDC
        int drawW, drawH;
        SDL3.SDL_GetWindowSizeInPixels(_window, &drawW, &drawH);
        float tw = (float)texW / drawW;   // half-width in NDC = texW / drawableW
        float th = (float)texH / drawH;
        float yOff = 0.4f;

        TexturedVertex[] tv = new[]
        {
            new TexturedVertex { PX = -tw, PY = yOff - th, TX = 0, TY = 1, CR = 1, CG = 1, CB = 1, CA = 1 },
            new TexturedVertex { PX =  tw, PY = yOff - th, TX = 1, TY = 1, CR = 1, CG = 1, CB = 1, CA = 1 },
            new TexturedVertex { PX = -tw, PY = yOff + th, TX = 0, TY = 0, CR = 1, CG = 1, CB = 1, CA = 1 },
            new TexturedVertex { PX =  tw, PY = yOff - th, TX = 1, TY = 1, CR = 1, CG = 1, CB = 1, CA = 1 },
            new TexturedVertex { PX =  tw, PY = yOff + th, TX = 1, TY = 0, CR = 1, CG = 1, CB = 1, CA = 1 },
            new TexturedVertex { PX = -tw, PY = yOff + th, TX = 0, TY = 0, CR = 1, CG = 1, CB = 1, CA = 1 },
        };
        uint tvSize = (uint)(sizeof(TexturedVertex) * tv.Length);
        var tvStgInfo = new SDL_GPUTransferBufferCreateInfo { size = tvSize, usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD };
        var tvStg = SDL3.SDL_CreateGPUTransferBuffer(_device, &tvStgInfo);
        void* tvMapped = (void*)SDL3.SDL_MapGPUTransferBuffer(_device, tvStg, false);
        fixed (TexturedVertex* src = tv)
            Buffer.MemoryCopy(src, tvMapped, tvSize, tvSize);
        SDL3.SDL_UnmapGPUTransferBuffer(_device, tvStg);
        var tvBufInfo = new SDL_GPUBufferCreateInfo { size = tvSize, usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX };
        _textVB = SDL3.SDL_CreateGPUBuffer(_device, &tvBufInfo);
        var tvUcmd = SDL3.SDL_AcquireGPUCommandBuffer(_device);
        var tvCp = SDL3.SDL_BeginGPUCopyPass(tvUcmd);
        var tvSl = new SDL_GPUTransferBufferLocation { transfer_buffer = tvStg, offset = 0 };
        var tvDr = new SDL_GPUBufferRegion { buffer = _textVB, offset = 0, size = tvSize };
        SDL3.SDL_UploadToGPUBuffer(tvCp, &tvSl, &tvDr, false); SDL3.SDL_EndGPUCopyPass(tvCp);
        SDL3.SDL_SubmitGPUCommandBuffer(tvUcmd); SDL3.SDL_ReleaseGPUTransferBuffer(_device, tvStg);

        Console.WriteLine("[GPU] Text texture ready.");
    }
}
