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
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.MeshData.Metadata);
        Assert.Equal(result.MeshData.Metadata.VertexCount, result.MeshData.Vertices.Count);
        Assert.Equal(result.MeshData.Metadata.TriangleCount, result.MeshData.Triangles.Count);
        Assert.Equal(result.MeshData.Metadata.MaterialCount, result.MeshData.MaterialSlots.Count);
        Assert.Single(result.MeshData.Metadata.TextureReferences);
        Assert.Equal("texture.bmp", result.MeshData.Metadata.TextureReferences[0].Path);

        Assert.Equal(3, result.MeshData.Vertices.Count);
        Assert.Single(result.MeshData.Triangles);
        Assert.Equal(0, result.MeshData.Triangles[0].V0);
        Assert.Equal(1, result.MeshData.Triangles[0].V1);
        Assert.Equal(2, result.MeshData.Triangles[0].V2);
        Assert.Equal(0, result.MeshData.Triangles[0].MaterialSlotIndex);
        Assert.Single(result.MeshData.MaterialSlots);
        Assert.Equal("Material 0", result.MeshData.MaterialSlots[0].MaterialName);
        Assert.Equal("texture.bmp", result.MeshData.MaterialSlots[0].TextureReference);

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
        Assert.Equal(2, result.MeshData.Triangles.Count);

        Assert.Equal(2, result.MeshData.Triangles[0].V0);
        Assert.Equal(0, result.MeshData.Triangles[0].V1);
        Assert.Equal(1, result.MeshData.Triangles[0].V2);
        Assert.Equal(0, result.MeshData.Triangles[0].MaterialSlotIndex);

        Assert.Equal(3, result.MeshData.Triangles[1].V0);
        Assert.Equal(2, result.MeshData.Triangles[1].V1);
        Assert.Equal(0, result.MeshData.Triangles[1].V2);
        Assert.Equal(0, result.MeshData.Triangles[1].MaterialSlotIndex);
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
    public async Task ReadAsync_WithTruncatedFaceBlock_ReturnsInvalidAndTruncatedStream()
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
    public async Task ReadAsync_WithMaterialSlotGeometry_SucceedsAndReturnsExpectedMaterialSlots()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "material_slot_geometry.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        Assert.NotNull(result.MeshData);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.MeshData.Metadata);
        Assert.Equal(result.MeshData.Metadata.VertexCount, result.MeshData.Vertices.Count);
        Assert.Equal(result.MeshData.Metadata.TriangleCount, result.MeshData.Triangles.Count);
        Assert.Equal(result.MeshData.Metadata.MaterialCount, result.MeshData.MaterialSlots.Count);
        Assert.Equal(2, result.MeshData.Metadata.TextureReferences.Count);
        Assert.Equal("texture1.bmp", result.MeshData.Metadata.TextureReferences[0].Path);
        Assert.Equal("texture2.bmp", result.MeshData.Metadata.TextureReferences[1].Path);

        Assert.Equal(3, result.MeshData.Vertices.Count);
        Assert.Equal(2, result.MeshData.MaterialSlots.Count);
        Assert.Equal("Material 0", result.MeshData.MaterialSlots[0].MaterialName);
        Assert.Equal("texture1.bmp", result.MeshData.MaterialSlots[0].TextureReference);
        Assert.Equal("Material 1", result.MeshData.MaterialSlots[1].MaterialName);
        Assert.Equal("texture2.bmp", result.MeshData.MaterialSlots[1].TextureReference);
        Assert.Single(result.MeshData.Triangles);
        Assert.Equal(0, result.MeshData.Triangles[0].V0);
        Assert.Equal(1, result.MeshData.Triangles[0].V1);
        Assert.Equal(2, result.MeshData.Triangles[0].V2);
        Assert.Equal(1, result.MeshData.Triangles[0].MaterialSlotIndex);
        Assert.All(result.MeshData.Triangles, t => Assert.True(t.MaterialSlotIndex >= 0 && t.MaterialSlotIndex < result.MeshData.MaterialSlots.Count));
    }

    [Fact]
    public async Task ReadAsync_WithInvalidIndexGeometry_ReturnsInvalidAndInvalidIndexDiagnostic()
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
    public async Task ReadAsync_WithLongIndexGeometry_SucceedsAndReturnsExpectedVertexCountAndTriangles()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "long_index_geometry.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        Assert.NotNull(result.MeshData);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.MeshData.Metadata);
        Assert.Equal(result.MeshData.Metadata.VertexCount, result.MeshData.Vertices.Count);
        Assert.Equal(result.MeshData.Metadata.TriangleCount, result.MeshData.Triangles.Count);
        Assert.Equal(result.MeshData.Metadata.MaterialCount, result.MeshData.MaterialSlots.Count);

        Assert.Equal(3, result.MeshData.Vertices.Count);
        Assert.Single(result.MeshData.Triangles);
        Assert.Equal(0, result.MeshData.Triangles[0].V0);
        Assert.Equal(1, result.MeshData.Triangles[0].V1);
        Assert.Equal(2, result.MeshData.Triangles[0].V2);
        Assert.Equal(0, result.MeshData.Triangles[0].MaterialSlotIndex);
        Assert.All(result.MeshData.Triangles, t => Assert.True(t.MaterialSlotIndex >= 0 && t.MaterialSlotIndex < result.MeshData.MaterialSlots.Count));
    }

    [Fact]
    public async Task ReadAsync_WithTruncatedLongFaceBlock_ReturnsInvalidAndTruncatedStream()
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
    public async Task ReadAsync_WithInvalidLongIndexGeometry_ReturnsInvalidAndInvalidIndexDiagnostic()
    {
        // Arrange
        var reader = new O3dGeometryReader();
        var filePath = Path.Combine(FixtureDirectory, "invalid_long_index_geometry.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dGeometryStatus.Invalid, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.InvalidIndex, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task ReadAsync_WithInvalidMaterialIndexGeometry_ReturnsInvalidAndInvalidIndexDiagnostic()
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
    public async Task ReadAsync_WithExcessiveStringLengthGeometry_ReturnsInvalidAndStringLengthExceeded()
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
    public async Task ReadAsync_WithTruncatedMaterialStringGeometry_ReturnsInvalidAndInvalidStringBounds()
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
    public async Task ReadAsync_CancelDuringMaterialLoop_ThrowsOperationCanceledException()
    {
        // Arrange
        var filePath = Path.Combine(FixtureDirectory, "material_slot_geometry.o3d");
        using var cts = new CancellationTokenSource();
        var reader = new TestO3dGeometryReader(path => 
            new CancellingStream(File.OpenRead(path), cts, cancelAfterBytesRead: 20));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await reader.ReadAsync(filePath, cts.Token)
        );
    }

    [Fact]
    public async Task ReadAsync_CancelDuringVertexLoop_ThrowsOperationCanceledException()
    {
        // Arrange
        var filePath = Path.Combine(FixtureDirectory, "material_slot_geometry.o3d");
        using var cts = new CancellationTokenSource();
        var reader = new TestO3dGeometryReader(path => 
            new CancellingStream(File.OpenRead(path), cts, cancelAfterBytesRead: 60));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await reader.ReadAsync(filePath, cts.Token)
        );
    }

    [Fact]
    public async Task ReadAsync_CancelDuringFaceLoop_ThrowsOperationCanceledException()
    {
        // Arrange
        var filePath = Path.Combine(FixtureDirectory, "material_slot_geometry.o3d");
        using var cts = new CancellationTokenSource();
        var reader = new TestO3dGeometryReader(path => 
            new CancellingStream(File.OpenRead(path), cts, cancelAfterBytesRead: 136));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await reader.ReadAsync(filePath, cts.Token)
        );
    }

    private class TestO3dGeometryReader : O3dGeometryReader
    {
        private readonly Func<string, Stream> _openFileFunc;

        public TestO3dGeometryReader(Func<string, Stream> openFileFunc)
        {
            _openFileFunc = openFileFunc;
        }

        protected override Stream OpenFile(string filePath)
        {
            return _openFileFunc(filePath);
        }
    }

    private class CancellingStream : Stream
    {
        private readonly Stream _inner;
        private readonly CancellationTokenSource _cts;
        private readonly int _cancelAfterBytesRead;
        private int _totalBytesRead;

        public CancellingStream(Stream inner, CancellationTokenSource cts, int cancelAfterBytesRead)
        {
            _inner = inner;
            _cts = cts;
            _cancelAfterBytesRead = cancelAfterBytesRead;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            _totalBytesRead += read;
            if (_totalBytesRead >= _cancelAfterBytesRead)
            {
                _cts.Cancel();
            }
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            int read = _inner.Read(buffer);
            _totalBytesRead += read;
            if (_totalBytesRead >= _cancelAfterBytesRead)
            {
                _cts.Cancel();
            }
            return read;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
