using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;
using OmsiStudio.OmsiFormat.Parser;

namespace OmsiStudio.OmsiFormat.Tests;

/// <summary>
/// Dedicated safety audit tests for O3D geometry reader.
/// Verifies DoS prevention, bounds checks, truncation safety, and invalid index protections.
/// </summary>
public class O3dGeometrySafetyAuditTests
{
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "O3dGeometry");
    private static readonly string MetadataFixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "O3d");

    [Fact]
    public async Task Audit_ExcessiveCounts_ReturnsSafetyLimitExceededAndNullMeshData()
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
    public async Task Audit_TruncatedVertexBlock_ReturnsTruncatedStreamAndNullMeshData()
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
    public async Task Audit_TruncatedStandardFaceBlock_ReturnsTruncatedStreamAndNullMeshData()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "truncated_face_block.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Invalid, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.TruncatedStream, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task Audit_TruncatedLongFaceBlock_ReturnsTruncatedStreamAndNullMeshData()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "truncated_long_face_block.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Invalid, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.TruncatedStream, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task Audit_InvalidVertexIndex_ReturnsInvalidIndexAndNullMeshData()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "invalid_index_geometry.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Invalid, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.InvalidIndex, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task Audit_InvalidMaterialSlotIndex_ReturnsInvalidIndexAndNullMeshData()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "invalid_material_index_geometry.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Invalid, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.InvalidIndex, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task Audit_ExcessiveStringLength_ReturnsStringLengthExceededAndNullMeshData()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "excessive_string_length_geometry.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Invalid, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.StringLengthExceeded, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task Audit_TruncatedMaterialString_ReturnsInvalidStringBoundsAndNullMeshData()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "truncated_material_string_geometry.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Invalid, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.InvalidStringBounds, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task Audit_EncryptedFile_ReturnsEncryptedStatusAndNullMeshData()
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
    public async Task Audit_SuccessfulFixture_HasEmptyDiagnosticsAndNonNullMeshData()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "minimal_valid_geometry.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        Assert.NotNull(result.MeshData);
        Assert.Empty(result.Diagnostics);
    }
}
