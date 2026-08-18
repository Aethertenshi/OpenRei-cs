namespace ReiStar.VisualEditor.Backend;

using System;
using System.Numerics;
using Hexa.NET.ImGui;
using SDL;

/// <summary>
/// Managed SDL_Renderer backend for Dear ImGui using ppy.SDL3-CS and Hexa.NET.ImGui.
/// Batches and renders ImGui draw lists directly with SDL_RenderGeometry without raw P/Invokes.
/// </summary>
public unsafe class ImGuiSdlRenderer : IDisposable
{
    private SDL_Renderer* _renderer;
    private SDL_Texture* _fontTexture;
    private SDL_Vertex[] _vertexCache = new SDL_Vertex[4096];
    private int[] _indexCache = new int[6144];
    private bool _isDisposed;

    public ImGuiSdlRenderer(SDL_Renderer* renderer)
    {
        _renderer = renderer;
        var io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;
    }

    private void UpdateFontAtlas()
    {
        var io = ImGui.GetIO();
        if (io.Fonts.TexData.IsNull) return;

        var texData = io.Fonts.TexData;
        if (texData.Status == ImTextureStatus.Ok) return;
        if (texData.Pixels == null || texData.Width <= 0 || texData.Height <= 0) return;

        if (_fontTexture == null)
        {
            _fontTexture = SDL3.SDL_CreateTexture(
                _renderer,
                SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888,
                SDL_TextureAccess.SDL_TEXTUREACCESS_STATIC,
                texData.Width,
                texData.Height
            );
            if (_fontTexture != null)
            {
                SDL3.SDL_SetTextureBlendMode(_fontTexture, SDL_BlendMode.SDL_BLENDMODE_BLEND);
                texData.SetTexID(new ImTextureID((void*)_fontTexture));
            }
        }

        if (_fontTexture != null)
        {
            SDL3.SDL_UpdateTexture(_fontTexture, null, (nint)texData.Pixels, texData.Width * 4);
            texData.SetTexID(new ImTextureID((void*)_fontTexture));
            texData.SetStatus(ImTextureStatus.Ok);
        }
    }

    public void RenderDrawData(ImDrawDataPtr drawData)
    {
        if (_renderer == null || drawData.Handle == null) return;

        UpdateFontAtlas();

        int fbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        int fbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        if (fbWidth <= 0 || fbHeight <= 0 || drawData.CmdListsCount == 0) return;

        SDL3.SDL_SetRenderDrawBlendMode(_renderer, SDL_BlendMode.SDL_BLENDMODE_BLEND);

        Vector2 clipOff = drawData.DisplayPos;
        Vector2 clipScale = drawData.FramebufferScale;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];
            int vtxCount = cmdList.VtxBuffer.Size;
            int idxCount = cmdList.IdxBuffer.Size;

            if (vtxCount <= 0 || idxCount <= 0) continue;

            if (_vertexCache.Length < vtxCount)
            {
                Array.Resize(ref _vertexCache, Math.Max(vtxCount, _vertexCache.Length * 2));
            }
            if (_indexCache.Length < idxCount)
            {
                Array.Resize(ref _indexCache, Math.Max(idxCount, _indexCache.Length * 2));
            }

            // Convert ImDrawVert to SDL_Vertex
            ImDrawVert* vtxSrc = cmdList.VtxBuffer.Data;
            for (int i = 0; i < vtxCount; i++)
            {
                ref var v = ref vtxSrc[i];
                uint col = v.Col;
                // Dear ImGui packed color (ABGR / RGBA format)
                float r = (col & 0xFF) / 255.0f;
                float g = ((col >> 8) & 0xFF) / 255.0f;
                float b = ((col >> 16) & 0xFF) / 255.0f;
                float a = ((col >> 24) & 0xFF) / 255.0f;

                _vertexCache[i] = new SDL_Vertex
                {
                    position = new SDL_FPoint { x = v.Pos.X, y = v.Pos.Y },
                    tex_coord = new SDL_FPoint { x = v.Uv.X, y = v.Uv.Y },
                    color = new SDL_FColor { r = r, g = g, b = b, a = a }
                };
            }

            // Convert indices
            ushort* idxSrc = (ushort*)cmdList.IdxBuffer.Data;
            for (int i = 0; i < idxCount; i++)
            {
                _indexCache[i] = idxSrc[i];
            }

            fixed (SDL_Vertex* vPtr = _vertexCache)
            fixed (int* iPtr = _indexCache)
            {
                for (int cmdI = 0; cmdI < cmdList.CmdBuffer.Size; cmdI++)
                {
                    ref var pcmd = ref cmdList.CmdBuffer.Data[cmdI];
                    if (pcmd.ElemCount == 0) continue;

                    // Compute clip rectangle
                    Vector2 clipMin = new Vector2((pcmd.ClipRect.X - clipOff.X) * clipScale.X, (pcmd.ClipRect.Y - clipOff.Y) * clipScale.Y);
                    Vector2 clipMax = new Vector2((pcmd.ClipRect.Z - clipOff.X) * clipScale.X, (pcmd.ClipRect.W - clipOff.Y) * clipScale.Y);

                    if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y) continue;

                    SDL_Rect clipRect = new SDL_Rect
                    {
                        x = (int)clipMin.X,
                        y = (int)clipMin.Y,
                        w = (int)(clipMax.X - clipMin.X),
                        h = (int)(clipMax.Y - clipMin.Y)
                    };

                    SDL3.SDL_SetRenderClipRect(_renderer, &clipRect);

                    SDL_Texture* tex = (pcmd.TexRef.TexData != null)
                        ? (SDL_Texture*)(void*)pcmd.TexRef.TexData->TexID
                        : (SDL_Texture*)(void*)pcmd.TexRef.TexID;

                    int* curIndices = iPtr + (int)pcmd.IdxOffset;
                    SDL_Vertex* curVertices = vPtr + (int)pcmd.VtxOffset;

                    SDL3.SDL_RenderGeometry(
                        _renderer,
                        tex,
                        curVertices,
                        vtxCount - (int)pcmd.VtxOffset,
                        curIndices,
                        (int)pcmd.ElemCount
                    );
                }
            }
        }

        SDL3.SDL_SetRenderClipRect(_renderer, null);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_fontTexture != null)
            {
                SDL3.SDL_DestroyTexture(_fontTexture);
                _fontTexture = null;
            }
            _renderer = null;
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
