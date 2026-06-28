using System;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// A no-op implementation of <see cref="IRendererHost"/> for use when no 3D renderer is available.
/// This ensures safety during unit tests and before full renderer wiring.
/// </summary>
public sealed class NullRendererHost : IRendererHost
{
    /// <inheritdoc />
    public RendererHostState State { get; private set; } = RendererHostState.Uninitialized;

    /// <inheritdoc />
    public O3dMeshData? CurrentMesh { get; private set; }

    /// <inheritdoc />
    public PreviewCameraState? CameraState { get; private set; }

    /// <inheritdoc />
    public PreviewRenderOptions? RenderOptions { get; private set; }

    /// <inheritdoc />
    public RendererHostSize CurrentSize { get; private set; } = new() { Width = 0, Height = 0 };

    /// <inheritdoc />
    public Task<RendererInitializationResult> InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        State = RendererHostState.Initialized;
        return Task.FromResult(RendererInitializationResult.Success());
    }

    /// <inheritdoc />
    public void Resize(RendererHostSize size)
    {
        ArgumentNullException.ThrowIfNull(size);
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        CurrentSize = size;
    }

    /// <inheritdoc />
    public void SetMesh(O3dMeshData? meshData)
    {
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        CurrentMesh = meshData;
    }

    /// <inheritdoc />
    public void SetCamera(PreviewCameraState cameraState)
    {
        ArgumentNullException.ThrowIfNull(cameraState);
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        CameraState = cameraState;
    }

    /// <inheritdoc />
    public void SetRenderOptions(PreviewRenderOptions renderOptions)
    {
        ArgumentNullException.ThrowIfNull(renderOptions);
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        RenderOptions = renderOptions;
    }

    /// <inheritdoc />
    public RenderFrameResult RenderFrame()
    {
        ObjectDisposedException.ThrowIf(State == RendererHostState.Disposed, this);
        if (State != RendererHostState.Initialized)
        {
            return RenderFrameResult.Failure("Renderer is not initialized.");
        }
        return RenderFrameResult.Success();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        State = RendererHostState.Disposed;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public bool DebugTriangleEnabled { get; set; }

    /// <inheritdoc />
    public bool LastFrameDrawAttempted => false;

    /// <inheritdoc />
    public int LastFrameUploadedVertexCount => 0;

    /// <inheritdoc />
    public int LastFrameUploadedIndexCount => 0;

    /// <inheritdoc />
    public string LastGlError => "NoError";
}
