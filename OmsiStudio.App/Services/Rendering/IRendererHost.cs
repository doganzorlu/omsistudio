using System;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// Defines the lifecycle and interface constraints for the 3D preview renderer.
/// Implementations do not swallow unexpected exceptions, ensuring proper error reporting.
/// </summary>
public interface IRendererHost : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the current state of the renderer host.
    /// </summary>
    RendererHostState State { get; }

    /// <summary>
    /// Gets the current mesh loaded into the renderer.
    /// </summary>
    O3dMeshData? CurrentMesh { get; }

    /// <summary>
    /// Gets the current state of the camera.
    /// </summary>
    PreviewCameraState? CameraState { get; }

    /// <summary>
    /// Gets the current render options.
    /// </summary>
    PreviewRenderOptions? RenderOptions { get; }

    /// <summary>
    /// Gets the current size of the viewport.
    /// </summary>
    RendererHostSize CurrentSize { get; }

    /// <summary>
    /// Asynchronously initializes the graphics context and renderer resources.
    /// </summary>
    /// <returns>A structured result indicating success or error.</returns>
    Task<RendererInitializationResult> InitializeAsync();

    /// <summary>
    /// Updates the viewport dimension state.
    /// </summary>
    /// <param name="size">The new size dimensions.</param>
    void Resize(RendererHostSize size);

    /// <summary>
    /// Sets or clears the active mesh to be previewed.
    /// </summary>
    /// <param name="meshData">The mesh geometry data, or null to clear.</param>
    void SetMesh(O3dMeshData? meshData);

    /// <summary>
    /// Updates the camera parameters for orbit, pan, or zoom calculations.
    /// </summary>
    /// <param name="cameraState">The camera state parameters.</param>
    void SetCamera(PreviewCameraState cameraState);

    /// <summary>
    /// Updates render options such as wireframe or bounding box toggles.
    /// </summary>
    /// <param name="renderOptions">The preview rendering options.</param>
    void SetRenderOptions(PreviewRenderOptions renderOptions);

    /// <summary>
    /// Renders a single frame. Should be called periodically during the rendering loop.
    /// </summary>
    /// <returns>A structured result indicating whether rendering completed successfully.</returns>
    RenderFrameResult RenderFrame();

    /// <summary>
    /// Gets or sets a value indicating whether to render the debug primitive (triangle).
    /// </summary>
    bool DebugTriangleEnabled { get; set; }

    /// <summary>
    /// Gets a value indicating whether a draw call was attempted in the last frame.
    /// </summary>
    bool LastFrameDrawAttempted { get; }

    /// <summary>
    /// Gets the vertex count uploaded in the last frame.
    /// </summary>
    int LastFrameUploadedVertexCount { get; }

    /// <summary>
    /// Gets the index count uploaded in the last frame.
    /// </summary>
    int LastFrameUploadedIndexCount { get; }

    /// <summary>
    /// Gets the last OpenGL error string or code.
    /// </summary>
    string LastGlError { get; }
}
