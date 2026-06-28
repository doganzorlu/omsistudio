using System;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Tests;

/// <summary>
/// Verifies the correct behavior, lifecycle states, property propagation, 
/// and exception rules for <see cref="NullRendererHost"/>.
/// </summary>
public class NullRendererHostTests
{
    [Fact]
    public async Task NullRendererHost_Lifecycle_TransitionStatesCorrectly()
    {
        // Arrange
        using var host = new NullRendererHost();
        Assert.Equal(RendererHostState.Uninitialized, host.State);

        // Act - Initialize
        var initResult = await host.InitializeAsync();

        // Assert
        Assert.True(initResult.IsSuccess);
        Assert.Null(initResult.ErrorMessage);
        Assert.Equal(RendererHostState.Initialized, host.State);

        // Act - Dispose
        host.Dispose();

        // Assert
        Assert.Equal(RendererHostState.Disposed, host.State);
    }

    [Fact]
    public async Task NullRendererHost_Resize_UpdatesSize()
    {
        // Arrange
        using var host = new NullRendererHost();
        await host.InitializeAsync();

        // Act
        var size = new RendererHostSize { Width = 800, Height = 600 };
        host.Resize(size);

        // Assert
        Assert.Equal(800, host.CurrentSize.Width);
        Assert.Equal(600, host.CurrentSize.Height);
    }

    [Fact]
    public async Task NullRendererHost_SetMesh_UpdatesMesh()
    {
        // Arrange
        using var host = new NullRendererHost();
        await host.InitializeAsync();

        // Act
        var mesh = new O3dMeshData();
        host.SetMesh(mesh);

        // Assert
        Assert.Equal(mesh, host.CurrentMesh);
    }

    [Fact]
    public async Task NullRendererHost_SetCamera_UpdatesCamera()
    {
        // Arrange
        using var host = new NullRendererHost();
        await host.InitializeAsync();

        // Act
        var camera = new PreviewCameraState();
        host.SetCamera(camera);

        // Assert
        Assert.Equal(camera, host.CameraState);
    }

    [Fact]
    public async Task NullRendererHost_SetRenderOptions_UpdatesOptions()
    {
        // Arrange
        using var host = new NullRendererHost();
        await host.InitializeAsync();

        // Act
        var options = new PreviewRenderOptions();
        host.SetRenderOptions(options);

        // Assert
        Assert.Equal(options, host.RenderOptions);
    }

    [Fact]
    public void NullRendererHost_RenderFrame_FailsWhenUninitialized()
    {
        // Arrange
        using var host = new NullRendererHost();

        // Act
        var renderResult = host.RenderFrame();

        // Assert
        Assert.False(renderResult.IsSuccess);
        Assert.Equal("Renderer is not initialized.", renderResult.ErrorMessage);
    }

    [Fact]
    public async Task NullRendererHost_RenderFrame_SucceedsWhenInitialized()
    {
        // Arrange
        using var host = new NullRendererHost();
        await host.InitializeAsync();

        // Act
        var renderResult = host.RenderFrame();

        // Assert
        Assert.True(renderResult.IsSuccess);
    }

    [Fact]
    public async Task NullRendererHost_OperationsAfterDispose_ThrowObjectDisposedException()
    {
        // Arrange
        var host = new NullRendererHost();
        host.Dispose();

        // Assert operations throw ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => host.Resize(new RendererHostSize { Width = 1, Height = 1 }));
        Assert.Throws<ObjectDisposedException>(() => host.SetMesh(new O3dMeshData()));
        Assert.Throws<ObjectDisposedException>(() => host.SetCamera(new PreviewCameraState()));
        Assert.Throws<ObjectDisposedException>(() => host.SetRenderOptions(new PreviewRenderOptions()));
        Assert.Throws<ObjectDisposedException>(() => host.RenderFrame());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.InitializeAsync());
    }

    [Fact]
    public async Task NullRendererHost_OperationsAfterDisposeAsync_ThrowObjectDisposedException()
    {
        // Arrange
        var host = new NullRendererHost();
        await host.DisposeAsync();

        // Assert operations throw ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => host.Resize(new RendererHostSize { Width = 1, Height = 1 }));
        Assert.Throws<ObjectDisposedException>(() => host.SetMesh(new O3dMeshData()));
        Assert.Throws<ObjectDisposedException>(() => host.SetCamera(new PreviewCameraState()));
        Assert.Throws<ObjectDisposedException>(() => host.SetRenderOptions(new PreviewRenderOptions()));
        Assert.Throws<ObjectDisposedException>(() => host.RenderFrame());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.InitializeAsync());
    }

    [Fact]
    public async Task NullRendererHost_DisposeAsync_DisposesSuccessfully()
    {
        // Arrange
        var host = new NullRendererHost();
        await host.InitializeAsync();
        
        // Act
        await host.DisposeAsync();

        // Assert
        Assert.Equal(RendererHostState.Disposed, host.State);
    }
}
