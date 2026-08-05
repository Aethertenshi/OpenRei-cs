namespace reistar.Renderer.SDL3;

using System;
using System.IO;
using System.Text;
using SDL;

public static unsafe class SpirvShaderLoader
{
    /// <summary>
    /// Loads a cross-platform SPIR-V shader module from byte array for SDL_GPU.
    /// </summary>
    public static SDL_GPUShader* LoadFromBytes(
        SDL_GPUDevice* device,
        byte[] spirvBytes,
        SDL_GPUShaderStage stage,
        string entrypoint = "main",
        uint numSamplers = 0,
        uint numUniformBuffers = 0,
        uint numStorageBuffers = 0,
        uint numStorageTextures = 0)
    {
        if (device == null) throw new ArgumentNullException(nameof(device));
        if (spirvBytes == null || spirvBytes.Length == 0) throw new ArgumentException("SPIR-V bytecode cannot be empty.", nameof(spirvBytes));

        byte[] entryBytes = Encoding.UTF8.GetBytes(entrypoint + "\0");

        fixed (byte* codePtr = spirvBytes)
        fixed (byte* entryPtr = entryBytes)
        {
            SDL_GPUShaderCreateInfo createInfo = new SDL_GPUShaderCreateInfo
            {
                code_size = (nuint)spirvBytes.Length,
                code = codePtr,
                entrypoint = entryPtr,
                format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, // Enforce cross-platform SPIR-V
                stage = stage,
                num_samplers = numSamplers,
                num_uniform_buffers = numUniformBuffers,
                num_storage_buffers = numStorageBuffers,
                num_storage_textures = numStorageTextures
            };

            SDL_GPUShader* shader = SDL3.SDL_CreateGPUShader(device, &createInfo);
            if (shader == null)
            {
                throw new InvalidOperationException($"Failed to create SPIR-V {stage} shader: {SDL3.SDL_GetError()}");
            }

            return shader;
        }
    }

    /// <summary>
    /// Loads a cross-platform SPIR-V (.spv) shader file from disk for SDL_GPU.
    /// </summary>
    public static SDL_GPUShader* LoadFromFile(
        SDL_GPUDevice* device,
        string filePath,
        SDL_GPUShaderStage stage,
        string entrypoint = "main",
        uint numSamplers = 0,
        uint numUniformBuffers = 0,
        uint numStorageBuffers = 0,
        uint numStorageTextures = 0)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        return LoadFromBytes(device, bytes, stage, entrypoint, numSamplers, numUniformBuffers, numStorageBuffers, numStorageTextures);
    }
}
