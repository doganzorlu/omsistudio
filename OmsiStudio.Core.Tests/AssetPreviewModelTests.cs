using Xunit;
using OmsiStudio.Core.Assets;
using System.Collections.Generic;

namespace OmsiStudio.Core.Tests;

/// <summary>
/// Unit tests verifying default value initialization, properties mapping,
/// and immutability characteristics of the preview domain models.
/// </summary>
public class AssetPreviewModelTests
{
    [Fact]
    public void AssetPreviewStatus_ShouldDefaultToUnknownZero()
    {
        // Assert
        Assert.Equal(0, (int)AssetPreviewStatus.Unknown);
        Assert.Equal(AssetPreviewStatus.Unknown, default(AssetPreviewStatus));
    }

    [Fact]
    public void DefaultModelInitialization_ShouldBeCorrect()
    {
        // Act
        var status = default(AssetPreviewStatus);
        var request = new AssetPreviewRequest();
        var result = new AssetPreviewResult();
        var cameraState = new PreviewCameraState();
        var options = new PreviewRenderOptions();
        var bounds = new MeshBounds();
        var vector = new PreviewVector3D();

        // Assert Vector
        Assert.Equal(0f, vector.X);
        Assert.Equal(0f, vector.Y);
        Assert.Equal(0f, vector.Z);

        // Assert Bounds
        Assert.NotNull(bounds.Min);
        Assert.NotNull(bounds.Max);
        Assert.NotNull(bounds.Center);
        Assert.NotNull(bounds.Size);

        // Assert Status
        Assert.Equal(AssetPreviewStatus.Unknown, status);

        // Assert Request
        Assert.Equal(string.Empty, request.AssetId);
        Assert.Equal(string.Empty, request.ModelPath);

        // Assert Result
        Assert.Equal(AssetPreviewStatus.Unknown, result.Status);
        Assert.Null(result.MeshData);
        Assert.Null(result.Bounds);
        Assert.Empty(result.Diagnostics);

        // Assert CameraState
        Assert.Equal(45f, cameraState.Yaw);
        Assert.Equal(-30f, cameraState.Pitch);
        Assert.Equal(5f, cameraState.Distance);
        Assert.NotNull(cameraState.PanOffset);

        // Assert RenderOptions
        Assert.False(options.WireframeEnabled);
        Assert.False(options.BoundingBoxEnabled);
        Assert.True(options.MaterialPreviewEnabled);
    }

    [Fact]
    public void InitializationWithValues_ShouldWorkAsExpected()
    {
        // Arrange & Act
        var vector = new PreviewVector3D { X = 1.5f, Y = 2.5f, Z = 3.5f };
        var bounds = new MeshBounds
        {
            Min = new PreviewVector3D { X = -1f, Y = -1f, Z = -1f },
            Max = new PreviewVector3D { X = 1f, Y = 1f, Z = 1f },
            Center = new PreviewVector3D { X = 0f, Y = 0f, Z = 0f },
            Size = new PreviewVector3D { X = 2f, Y = 2f, Z = 2f }
        };
        var request = new AssetPreviewRequest { AssetId = "scenery_obj_1", ModelPath = "model/cube.o3d" };
        var result = new AssetPreviewResult
        {
            Status = AssetPreviewStatus.Success,
            MeshData = new O3dMeshData(),
            Bounds = bounds,
            Diagnostics = new List<O3dDiagnostic> { new() { Severity = O3dDiagnosticSeverity.Warning, Message = "Test Warning" } }
        };
        var cameraState = new PreviewCameraState { Yaw = 90f, Pitch = -45f, Distance = 10f, PanOffset = vector };
        var options = new PreviewRenderOptions { WireframeEnabled = true, BoundingBoxEnabled = true, MaterialPreviewEnabled = false };

        // Assert Vector
        Assert.Equal(1.5f, vector.X);
        Assert.Equal(2.5f, vector.Y);
        Assert.Equal(3.5f, vector.Z);

        // Assert Bounds
        Assert.Equal(-1f, bounds.Min.X);
        Assert.Equal(1f, bounds.Max.X);
        Assert.Equal(0f, bounds.Center.X);
        Assert.Equal(2f, bounds.Size.X);

        // Assert Request
        Assert.Equal("scenery_obj_1", request.AssetId);
        Assert.Equal("model/cube.o3d", request.ModelPath);

        // Assert Result
        Assert.Equal(AssetPreviewStatus.Success, result.Status);
        Assert.NotNull(result.MeshData);
        Assert.Equal(bounds, result.Bounds);
        Assert.Single(result.Diagnostics);
        Assert.Equal("Test Warning", result.Diagnostics[0].Message);

        // Assert CameraState
        Assert.Equal(90f, cameraState.Yaw);
        Assert.Equal(-45f, cameraState.Pitch);
        Assert.Equal(10f, cameraState.Distance);
        Assert.Equal(vector, cameraState.PanOffset);

        // Assert RenderOptions
        Assert.True(options.WireframeEnabled);
        Assert.True(options.BoundingBoxEnabled);
        Assert.False(options.MaterialPreviewEnabled);
    }
}
