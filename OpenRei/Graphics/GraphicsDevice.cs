using SDL;

namespace OpenRei.Graphics;

/// <summary>
/// Controls SDL_GPU hardware device, pipeline states, and low-latency swapchain presentation modes.
/// </summary>
public unsafe class GraphicsDevice : IDisposable
{
    private SDL_GPUDevice* _device;
    private SDL_Window* _window;
    private bool _isDisposed;

    public SDL_GPUDevice* DeviceHandle => _device;
    public bool IsInitialized => _device != null;

    public GraphicsDevice(SDL_Window* window)
    {
        _window = window;

        // Create SDL_GPU Device supporting Vulkan, Direct3D 12, and Metal
        _device = SDL3.SDL_CreateGPUDevice(
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV |
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL |
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL,
            true,
            (byte*)null
        );

        if (_device == null)
        {
            Console.WriteLine($"[SDL_GPU Warning] Could not initialize native GPU device: {SDL3.SDL_GetError()}");
            return;
        }

        // Claim window swapchain
        if (!SDL3.SDL_ClaimWindowForGPUDevice(_device, _window))
        {
            Console.WriteLine($"[SDL_GPU Warning] Could not claim window for GPU device: {SDL3.SDL_GetError()}");
            return;
        }

        // Set low-latency Mailbox triple buffering swapchain mode
        SDL3.SDL_SetGPUSwapchainParameters(
            _device,
            _window,
            SDL_GPUSwapchainComposition.SDL_GPU_SWAPCHAINCOMPOSITION_SDR,
            SDL_GPUPresentMode.SDL_GPU_PRESENTMODE_MAILBOX
        );

        Console.WriteLine("[SDL_GPU] Hardware Graphics Device initialized with Mailbox Triple Buffering.");
    }

    public void BeginFrame(out SDL_GPUCommandBuffer* cmdBuffer, out SDL_GPUTexture* swapchainTexture)
    {
        cmdBuffer = null;
        swapchainTexture = null;

        if (!IsInitialized) return;

        cmdBuffer = SDL3.SDL_AcquireGPUCommandBuffer(_device);
        if (cmdBuffer == null) return;

        uint width = 0, height = 0;
        SDL_GPUTexture* swapTexture = null;
        if (!SDL3.SDL_WaitAndAcquireGPUSwapchainTexture(cmdBuffer, _window, &swapTexture, &width, &height))
        {
            return;
        }

        swapchainTexture = swapTexture;
    }

    public void EndFrame(SDL_GPUCommandBuffer* cmdBuffer)
    {
        if (cmdBuffer != null)
        {
            SDL3.SDL_SubmitGPUCommandBuffer(cmdBuffer);
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_device != null && _window != null)
            {
                SDL3.SDL_ReleaseWindowFromGPUDevice(_device, _window);
                SDL3.SDL_DestroyGPUDevice(_device);
                _device = null;
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
