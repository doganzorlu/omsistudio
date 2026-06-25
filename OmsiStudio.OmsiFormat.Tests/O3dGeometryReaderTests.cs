using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;
using OmsiStudio.OmsiFormat.Parser;

namespace OmsiStudio.OmsiFormat.Tests;

public class O3dGeometryReaderTests
{
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "O3dGeometry");
    private static readonly string MetadataFixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "O3d");

    [Fact]
    public async Task ReadAsync_WithMinimalValidGeometry_SucceedsAndReturnsVertices()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "minimal_valid_geometry.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        Assert.NotNull(result.MeshData);
        Assert.Equal(3, result.MeshData.Vertices.Count);
        Assert.Empty(result.MeshData.Triangles);
        Assert.Empty(result.MeshData.MaterialSlots);

        // Check first vertex layout values
        var v0 = result.MeshData.Vertices[0];
        Assert.Equal(0f, v0.X);
        Assert.Equal(0f, v0.Y);
        Assert.Equal(0f, v0.Z);
        Assert.Equal(0f, v0.Normal.X);
        Assert.Equal(0f, v0.Normal.Y);
        Assert.Equal(1f, v0.Normal.Z);
        Assert.Equal(0f, v0.Uv.U);
        Assert.Equal(0f, v0.Uv.V);

        var v1 = result.MeshData.Vertices[1];
        Assert.Equal(1f, v1.X);
        Assert.Equal(0f, v1.Y);
        Assert.Equal(0f, v1.Z);
        Assert.Equal(1f, v1.Uv.U);
    }

    [Fact]
    public async Task ReadAsync_WithMultiTriangleGeometry_SucceedsAndReturnsExpectedVertexCount()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "multi_triangle_geometry.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        Assert.NotNull(result.MeshData);
        Assert.Equal(4, result.MeshData.Vertices.Count);
        Assert.Empty(result.MeshData.Triangles); // Triangles not parsed in this task
    }

    [Fact]
    public async Task ReadAsync_WithTruncatedVertexBlock_ReturnsInvalidAndTruncatedStream()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "truncated_vertex_block.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Invalid, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.TruncatedStream, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task ReadAsync_WithExcessiveCounts_ReturnsInvalidAndSafetyLimitExceeded()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "excessive_geometry_counts.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Invalid, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task ReadAsync_WithEncryptedFile_ReturnsEncryptedStatus()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(MetadataFixtureDirectory, "encrypted_marker.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Encrypted, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.EncryptedFile, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task ReadAsync_WithTruncatedFaceBlock_SucceedsBecauseFacesAreNotReadYet()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "truncated_face_block.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        Assert.NotNull(result.MeshData);
        Assert.Equal(3, result.MeshData.Vertices.Count);
        Assert.Empty(result.MeshData.Triangles);
    }

    [Fact]
    public async Task ReadAsync_WithPreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "minimal_valid_geometry.o3d");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await reader.ReadAsync(filePath, cts.Token)
        );
    }

    [Fact]
    public async Task ReadAsync_WithMaterialSlotGeometry_SucceedsAndLeavesMaterialSlotsEmpty()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "material_slot_geometry.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        Assert.NotNull(result.MeshData);
        Assert.Equal(3, result.MeshData.Vertices.Count);
        Assert.Empty(result.MeshData.MaterialSlots); // Confirms we successfully skipped multiple strings and kept MaterialSlots empty
        Assert.Empty(result.MeshData.Triangles);
    }
}
