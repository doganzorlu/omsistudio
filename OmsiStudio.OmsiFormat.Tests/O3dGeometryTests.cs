using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.OmsiFormat.Tests;

public class O3dGeometryTests
{
    [Fact]
    public void O3dGeometryStatus_ShouldDefaultToUnknownZero()
    {
        // Assert
        Assert.Equal(0, (int)O3dGeometryStatus.Unknown);
        Assert.Equal(O3dGeometryStatus.Unknown, default(O3dGeometryStatus));
    }

    [Fact]
    public void DefaultModelInitialization_ShouldBeCorrect()
    {
        // Arrange & Act
        var uv = new O3dUv();
        var normal = new O3dNormal();
        var vertex = new O3dVertex();
        var materialSlot = new O3dMaterialSlot();
        var triangle = new O3dTriangle();
        var meshData = new O3dMeshData();
        var readResult = new O3dGeometryReadResult();

        // Assert UV
        Assert.Equal(0f, uv.U);
        Assert.Equal(0f, uv.V);

        // Assert Normal
        Assert.Equal(0f, normal.X);
        Assert.Equal(0f, normal.Y);
        Assert.Equal(0f, normal.Z);

        // Assert Vertex
        Assert.Equal(0f, vertex.X);
        Assert.Equal(0f, vertex.Y);
        Assert.Equal(0f, vertex.Z);
        Assert.NotNull(vertex.Normal);
        Assert.NotNull(vertex.Uv);

        // Assert MaterialSlot
        Assert.Equal(string.Empty, materialSlot.MaterialName);
        Assert.Null(materialSlot.TextureReference);

        // Assert Triangle
        Assert.Equal(0, triangle.V0);
        Assert.Equal(0, triangle.V1);
        Assert.Equal(0, triangle.V2);
        Assert.Null(triangle.MaterialSlotIndex);

        // Assert MeshData
        Assert.Empty(meshData.Vertices);
        Assert.Empty(meshData.Triangles);
        Assert.Empty(meshData.MaterialSlots);
        Assert.Null(meshData.Metadata);

        // Assert ReadResult
        Assert.Null(readResult.MeshData);
        Assert.Equal(O3dGeometryStatus.Unknown, readResult.Status);
        Assert.Empty(readResult.Diagnostics);
    }

    [Fact]
    public void ObjectInitializer_WithProperties_ShouldMapCorrectly()
    {
        // Arrange
        var testMetadata = new O3dMetadata
        {
            Version = O3dFormatVersion.Version3,
            IsEncrypted = false
        };

        var vertices = new List<O3dVertex>
        {
            new()
            {
                X = 1f, Y = 2f, Z = 3f,
                Normal = new O3dNormal { X = 0f, Y = 1f, Z = 0f },
                Uv = new O3dUv { U = 0.5f, V = 0.5f }
            }
        };

        var triangles = new List<O3dTriangle>
        {
            new() { V0 = 0, V1 = 1, V2 = 2, MaterialSlotIndex = 1 }
        };

        var materialSlots = new List<O3dMaterialSlot>
        {
            new() { MaterialName = "Material1", TextureReference = "tex.png" }
        };

        var diagnostics = new List<O3dDiagnostic>
        {
            new()
            {
                Severity = O3dDiagnosticSeverity.Warning,
                Code = O3dDiagnosticCode.InvalidHeader,
                Message = "Non-fatal warning."
            }
        };

        // Act
        var meshData = new O3dMeshData
        {
            Vertices = vertices,
            Triangles = triangles,
            MaterialSlots = materialSlots,
            Metadata = testMetadata
        };

        var readResult = new O3dGeometryReadResult
        {
            MeshData = meshData,
            Status = O3dGeometryStatus.Success,
            Diagnostics = diagnostics
        };

        // Assert MeshData
        Assert.NotNull(readResult.MeshData);
        Assert.Same(vertices, readResult.MeshData.Vertices);
        Assert.Same(triangles, readResult.MeshData.Triangles);
        Assert.Same(materialSlots, readResult.MeshData.MaterialSlots);
        Assert.Same(testMetadata, readResult.MeshData.Metadata);

        // Assert ReadResult
        Assert.Equal(O3dGeometryStatus.Success, readResult.Status);
        Assert.Same(diagnostics, readResult.Diagnostics);

        // Assert Individual Property Values
        var firstVertex = readResult.MeshData.Vertices[0];
        Assert.Equal(1f, firstVertex.X);
        Assert.Equal(2f, firstVertex.Y);
        Assert.Equal(3f, firstVertex.Z);
        Assert.Equal(1f, firstVertex.Normal.Y);
        Assert.Equal(0.5f, firstVertex.Uv.U);

        var firstTriangle = readResult.MeshData.Triangles[0];
        Assert.Equal(0, firstTriangle.V0);
        Assert.Equal(1, firstTriangle.V1);
        Assert.Equal(2, firstTriangle.V2);
        Assert.Equal(1, firstTriangle.MaterialSlotIndex);

        var firstSlot = readResult.MeshData.MaterialSlots[0];
        Assert.Equal("Material1", firstSlot.MaterialName);
        Assert.Equal("tex.png", firstSlot.TextureReference);
    }

    [Fact]
    public async Task FakeReader_CanImplementIO3dGeometryReader_AndReturnsReadResult()
    {
        // Arrange
        IO3dGeometryReader reader = new FakeO3dGeometryReader();
        var cts = new System.Threading.CancellationTokenSource();

        // Act
        var result = await reader.ReadAsync("fake_path.o3d", cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        Assert.NotNull(result.MeshData);
        Assert.Empty(result.MeshData.Vertices);
        Assert.Empty(result.Diagnostics);
    }

    private class FakeO3dGeometryReader : IO3dGeometryReader
    {
        public Task<O3dGeometryReadResult> ReadAsync(string filePath, System.Threading.CancellationToken cancellationToken = default)
        {
            var result = new O3dGeometryReadResult
            {
                MeshData = new O3dMeshData(),
                Status = O3dGeometryStatus.Success,
                Diagnostics = []
            };
            return Task.FromResult(result);
        }
    }

    [Fact]
    public void O3dGeometryFixtures_ExistAndHaveCorrectByteSizes()
    {
        var baseDir = AppContext.BaseDirectory;
        var outputFixturesDir = Path.Combine(baseDir, "Fixtures", "O3dGeometry");

        var expectedSizes = new Dictionary<string, long>
        {
            { "minimal_valid_geometry.o3d", 139 },
            { "multi_triangle_geometry.o3d", 179 },
            { "invalid_index_geometry.o3d", 139 },
            { "truncated_vertex_block.o3d", 67 },
            { "truncated_face_block.o3d", 131 },
            { "excessive_geometry_counts.o3d", 35 },
            { "material_slot_geometry.o3d", 156 },
            { "long_index_geometry.o3d", 145 },
            { "truncated_long_face_block.o3d", 131 },
            { "invalid_long_index_geometry.o3d", 145 },
            { "invalid_material_index_geometry.o3d", 139 },
            { "excessive_string_length_geometry.o3d", 24 },
            { "truncated_material_string_geometry.o3d", 24 }
        };

        foreach (var pair in expectedSizes)
        {
            var outputPath = Path.Combine(outputFixturesDir, pair.Key);
            Assert.True(File.Exists(outputPath), $"Fixture {pair.Key} should exist in test output at: {outputPath}");
            var fileInfo = new FileInfo(outputPath);
            Assert.Equal(pair.Value, fileInfo.Length);
            
            // Intentionally truncated/excessive fixtures should remain small in file size
            if (pair.Key == "truncated_vertex_block.o3d" || pair.Key == "truncated_face_block.o3d" || 
                pair.Key == "truncated_long_face_block.o3d" || pair.Key == "excessive_geometry_counts.o3d" ||
                pair.Key == "excessive_string_length_geometry.o3d" || pair.Key == "truncated_material_string_geometry.o3d")
            {
                Assert.True(fileInfo.Length < 200, $"{pair.Key} should remain small.");
            }
        }
    }
}

