using System;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Tests;

/// <summary>
/// Verifies the correct behavior, safety checks, and error handling 
/// of <see cref="OpenGlRendererHost"/> when no GL context is configured.
/// </summary>
public class OpenGlRendererHostTests
{
    [Fact]
    public async Task OpenGlRendererHost_WithoutGl_InitializationFailsSafely()
    {
        // Arrange
        using var host = new OpenGlRendererHost();
        Assert.Equal(RendererHostState.Uninitialized, host.State);

        // Act
        var initResult = await host.InitializeAsync();

        // Assert
        Assert.False(initResult.IsSuccess);
        Assert.Contains("OpenGL API has not been configured", initResult.ErrorMessage);
        Assert.Equal(RendererHostState.Failed, host.State);
    }

    [Fact]
    public void OpenGlRendererHost_WithoutGl_RenderFrameFailsSafely()
    {
        // Arrange
        using var host = new OpenGlRendererHost();

        // Act
        var renderResult = host.RenderFrame();

        // Assert
        Assert.False(renderResult.IsSuccess);
        Assert.Equal("Renderer is not initialized.", renderResult.ErrorMessage);
    }

    [Fact]
    public void OpenGlRendererHost_Resize_StoresDimensions()
    {
        // Arrange
        using var host = new OpenGlRendererHost();
        var size = new RendererHostSize { Width = 100, Height = 200 };

        // Act
        host.Resize(size);

        // Assert
        Assert.Equal(100, host.CurrentSize.Width);
        Assert.Equal(200, host.CurrentSize.Height);
    }

    [Fact]
    public void OpenGlRendererHost_SetMesh_StoresMesh()
    {
        // Arrange
        using var host = new OpenGlRendererHost();
        var mesh = new O3dMeshData();

        // Act
        host.SetMesh(mesh);

        // Assert
        Assert.Equal(mesh, host.CurrentMesh);
    }

    [Fact]
    public async Task OpenGlRendererHost_OperationsAfterDispose_ThrowObjectDisposedException()
    {
        // Arrange
        var host = new OpenGlRendererHost();
        host.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => host.Resize(new RendererHostSize { Width = 1, Height = 1 }));
        Assert.Throws<ObjectDisposedException>(() => host.SetMesh(new O3dMeshData()));
        Assert.Throws<ObjectDisposedException>(() => host.SetCamera(new PreviewCameraState()));
        Assert.Throws<ObjectDisposedException>(() => host.SetRenderOptions(new PreviewRenderOptions()));
        Assert.Throws<ObjectDisposedException>(() => host.RenderFrame());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.InitializeAsync());
    }

    [Fact]
    public async Task OpenGlRendererHost_OperationsAfterDisposeAsync_ThrowObjectDisposedException()
    {
        // Arrange
        var host = new OpenGlRendererHost();
        await host.DisposeAsync();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => host.Resize(new RendererHostSize { Width = 1, Height = 1 }));
        Assert.Throws<ObjectDisposedException>(() => host.SetMesh(new O3dMeshData()));
        Assert.Throws<ObjectDisposedException>(() => host.SetCamera(new PreviewCameraState()));
        Assert.Throws<ObjectDisposedException>(() => host.SetRenderOptions(new PreviewRenderOptions()));
        Assert.Throws<ObjectDisposedException>(() => host.RenderFrame());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.InitializeAsync());
    }

    [Fact]
    public async Task OpenGlRendererHost_DetachGl_FromFailedState_ResetsToUninitialized()
    {
        // Arrange
        using var host = new OpenGlRendererHost();
        // Initialize without GL to put it in Failed state
        var initResult = await host.InitializeAsync();
        Assert.False(initResult.IsSuccess);
        Assert.Equal(RendererHostState.Failed, host.State);

        // Act - Detach GL
        host.DetachGl();

        // Assert - Transitions back to Uninitialized
        Assert.Equal(RendererHostState.Uninitialized, host.State);
    }

    [Fact]
    public void OpenGlRendererHost_NullOrEmptyMesh_WhenNotInitialized_DoesNotCrash()
    {
        // Arrange
        using var host = new OpenGlRendererHost();

        // Act
        host.SetMesh(null);
        var renderResult1 = host.RenderFrame();

        var emptyMesh = new O3dMeshData();
        host.SetMesh(emptyMesh);
        var renderResult2 = host.RenderFrame();

        // Assert
        Assert.False(renderResult1.IsSuccess);
        Assert.False(renderResult2.IsSuccess);
    }

    [Fact]
    public void OpenGlRendererHost_UploadedCounts_AreInitiallyZeroAndResetOnCleanup()
    {
        // Arrange
        using var host = new OpenGlRendererHost();
        
        // Assert initial state
        Assert.Equal(0, host.UploadedVertexCount);
        Assert.Equal(0, host.UploadedIndexCount);

        // Act
        var mesh = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new O3dVertex { X = 1, Y = 2, Z = 3 } },
            Triangles = new List<O3dTriangle> { new O3dTriangle { V0 = 0, V1 = 0, V2 = 0 } }
        };
        host.SetMesh(mesh);

        // Even after SetMesh, counts should be 0 because UploadMesh is not run yet
        Assert.Equal(0, host.UploadedVertexCount);
        Assert.Equal(0, host.UploadedIndexCount);

        // Act - Detach GL (which runs CleanupMeshBuffers)
        host.DetachGl();

        // Assert - still 0
        Assert.Equal(0, host.UploadedVertexCount);
        Assert.Equal(0, host.UploadedIndexCount);
    }

    [Fact]
    public void OpenGlRendererHost_DebugTriangleMode_DefaultsAndState_AreCorrect()
    {
        // Arrange
        using var host = new OpenGlRendererHost();

        // Assert defaults
        Assert.False(host.DebugTriangleEnabled);
        Assert.False(host.LastFrameDrawAttempted);
        Assert.Equal(0, host.LastFrameUploadedVertexCount);
        Assert.Equal(0, host.LastFrameUploadedIndexCount);
        Assert.Equal("NoError", host.LastGlError);

        // Act
        host.DebugTriangleEnabled = true;

        // Assert state change
        Assert.True(host.DebugTriangleEnabled);
    }
}
