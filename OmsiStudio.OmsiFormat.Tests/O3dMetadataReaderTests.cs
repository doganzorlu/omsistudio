using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;
using OmsiStudio.OmsiFormat.Parser;

namespace OmsiStudio.OmsiFormat.Tests;

public class O3dMetadataReaderTests
{
    private static readonly string FixtureDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "O3d");

    [Fact]
    public void O3dMetadataReader_IsAssignableToIO3dMetadataReader()
    {
        var reader = new O3dMetadataReader();
        Assert.True(reader is IO3dMetadataReader);
    }

    [Fact]
    public void Read_WithValidV3Header_SucceedsAndReturnsExpectedCountsAndTextures()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var filePath = Path.Combine(FixtureDirectory, "valid_v3_header.o3d");

        // Act
        var result = reader.Read(filePath);

        // Assert
        Assert.Equal(O3dMetadataStatus.Success, result.Status);
        Assert.NotNull(result.Metadata);
        Assert.Equal(O3dFormatVersion.Version3, result.Metadata.Version);
        Assert.Equal(3, result.Metadata.RawVersion);
        Assert.False(result.Metadata.IsEncrypted);
        Assert.Equal(1, result.Metadata.MeshCount);
        Assert.Equal(100, result.Metadata.VertexCount);
        Assert.Equal(50, result.Metadata.TriangleCount);
        Assert.Equal(1, result.Metadata.MaterialCount);
        Assert.Single(result.Metadata.TextureReferences);
        Assert.Equal("texture.bmp", result.Metadata.TextureReferences[0].Path);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task ReadAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var filePath = Path.Combine(FixtureDirectory, "valid_v3_header.o3d");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await reader.ReadAsync(filePath, cts.Token)
        );
    }

    [Fact]
    public async Task ReadAsync_WithValidV3Header_SucceedsAndReturnsExpectedCountsAndTextures()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var filePath = Path.Combine(FixtureDirectory, "valid_v3_header.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dMetadataStatus.Success, result.Status);
        Assert.NotNull(result.Metadata);
        Assert.Equal(O3dFormatVersion.Version3, result.Metadata.Version);
        Assert.Equal(3, result.Metadata.RawVersion);
        Assert.False(result.Metadata.IsEncrypted);
        Assert.Equal(1, result.Metadata.MeshCount);
        Assert.Equal(100, result.Metadata.VertexCount);
        Assert.Equal(50, result.Metadata.TriangleCount);
        Assert.Equal(1, result.Metadata.MaterialCount);
        Assert.Single(result.Metadata.TextureReferences);
        Assert.Equal("texture.bmp", result.Metadata.TextureReferences[0].Path);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task ReadAsync_WithEncryptedMarker_ReturnsEncryptedStatusAndDefaultCounts()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var filePath = Path.Combine(FixtureDirectory, "encrypted_marker.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dMetadataStatus.Encrypted, result.Status);
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata.IsEncrypted);
        Assert.Equal(O3dFormatVersion.Unknown, result.Metadata.Version);
        Assert.Equal(0, result.Metadata.RawVersion);
        Assert.Equal(0, result.Metadata.MeshCount);
        Assert.Equal(0, result.Metadata.VertexCount);
        Assert.Equal(0, result.Metadata.TriangleCount);
        Assert.Equal(0, result.Metadata.MaterialCount);
        Assert.Empty(result.Metadata.TextureReferences);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.EncryptedFile, result.Diagnostics[0].Code);
        Assert.Equal(O3dDiagnosticSeverity.Error, result.Diagnostics[0].Severity);
    }

    [Fact]
    public async Task ReadAsync_WithTooShortFile_ReturnsTruncatedDiagnostics()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"O3dTruncTest_{Guid.NewGuid():N}.o3d");
        await File.WriteAllBytesAsync(tempFile, [0x03, 0x00]); // 2 bytes, V3 requires 4 bytes total to check second word

        try
        {
            // Act
            var result = await reader.ReadAsync(tempFile);

            // Assert
            Assert.Equal(O3dMetadataStatus.Invalid, result.Status);
            Assert.Single(result.Diagnostics);
            Assert.Equal(O3dDiagnosticCode.TruncatedStream, result.Diagnostics[0].Code);
            Assert.Equal(O3dDiagnosticSeverity.Error, result.Diagnostics[0].Severity);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_WithUnsupportedVersion_ReturnsUnsupportedStatusAndDefaultCounts()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"O3dUnsuppTest_{Guid.NewGuid():N}.o3d");
        await File.WriteAllBytesAsync(tempFile, [0x05, 0x00, 0x00, 0x00]); // Version 5

        try
        {
            // Act
            var result = await reader.ReadAsync(tempFile);

            // Assert
            Assert.Equal(O3dMetadataStatus.Unsupported, result.Status);
            Assert.Null(result.Metadata);
            Assert.Single(result.Diagnostics);
            Assert.Equal(O3dDiagnosticCode.UnsupportedVersion, result.Diagnostics[0].Code);
            Assert.Equal(O3dDiagnosticSeverity.Warning, result.Diagnostics[0].Severity);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_WithLegacyVersion_ReturnsLegacyStatusAndParsedCountsAndTextures()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"O3dLegacyTest_{Guid.NewGuid():N}.o3d");
        // Legacy file: version=2 (2 bytes), meshCount=1 (2 bytes), vertexCount=100 (2 bytes), triangleCount=50 (2 bytes), materialCount=1 (2 bytes), texLen=11 (2 bytes), texture="texture.bmp"
        await File.WriteAllBytesAsync(tempFile, [
            0x02, 0x00,
            0x01, 0x00,
            0x64, 0x00,
            0x32, 0x00,
            0x01, 0x00,
            0x0b, 0x00,
            0x74, 0x65, 0x78, 0x74, 0x75, 0x72, 0x65, 0x2e, 0x62, 0x6d, 0x70
        ]);

        try
        {
            // Act
            var result = await reader.ReadAsync(tempFile);

            // Assert
            Assert.Equal(O3dMetadataStatus.Success, result.Status);
            Assert.NotNull(result.Metadata);
            Assert.Equal(O3dFormatVersion.Legacy, result.Metadata.Version);
            Assert.Equal(2, result.Metadata.RawVersion);
            Assert.False(result.Metadata.IsEncrypted);
            Assert.Equal(1, result.Metadata.MeshCount);
            Assert.Equal(100, result.Metadata.VertexCount);
            Assert.Equal(50, result.Metadata.TriangleCount);
            Assert.Equal(1, result.Metadata.MaterialCount);
            Assert.Single(result.Metadata.TextureReferences);
            Assert.Equal("texture.bmp", result.Metadata.TextureReferences[0].Path);
            Assert.Empty(result.Diagnostics);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_WithExcessiveCountFixture_ReturnsSafetyDiagnostic()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var filePath = Path.Combine(FixtureDirectory, "dos_excessive_count.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dMetadataStatus.Invalid, result.Status);
        Assert.Null(result.Metadata);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, result.Diagnostics[0].Code);
        Assert.Equal(O3dDiagnosticSeverity.Error, result.Diagnostics[0].Severity);
        Assert.Contains("exceeded safety limit", result.Diagnostics[0].Message);
    }

    [Fact]
    public async Task ReadAsync_WithInvalidStringBounds_ReturnsInvalidStringBoundsDiagnostic()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var filePath = Path.Combine(FixtureDirectory, "invalid_string_bounds.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dMetadataStatus.Invalid, result.Status);
        Assert.Null(result.Metadata);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.InvalidStringBounds, result.Diagnostics[0].Code);
        Assert.Equal(O3dDiagnosticSeverity.Error, result.Diagnostics[0].Severity);
    }

    [Fact]
    public async Task ReadAsync_WithStringLengthExceedingLimit_ReturnsStringLengthExceededDiagnostic()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"O3dStrLimitTest_{Guid.NewGuid():N}.o3d");
        // version=3, meshes=1, vertices=100, triangles=50, materials=1, string_len=1025 (exceeds MaxStringLength=1024)
        byte[] stringLenBytes = BitConverter.GetBytes((uint)1025);
        byte[] headerBytes = [
            0x03, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x64, 0x00, 0x00, 0x00,
            0x32, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00
        ];
        byte[] fileBytes = new byte[headerBytes.Length + stringLenBytes.Length];
        Buffer.BlockCopy(headerBytes, 0, fileBytes, 0, headerBytes.Length);
        Buffer.BlockCopy(stringLenBytes, 0, fileBytes, headerBytes.Length, stringLenBytes.Length);

        await File.WriteAllBytesAsync(tempFile, fileBytes);

        try
        {
            // Act
            var result = await reader.ReadAsync(tempFile);

            // Assert
            Assert.Equal(O3dMetadataStatus.Invalid, result.Status);
            Assert.Null(result.Metadata);
            Assert.Single(result.Diagnostics);
            Assert.Equal(O3dDiagnosticCode.StringLengthExceeded, result.Diagnostics[0].Code);
            Assert.Equal(O3dDiagnosticSeverity.Error, result.Diagnostics[0].Severity);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_WithMissingFile_ReturnsFailedStatus()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var filePath = Path.Combine(FixtureDirectory, "non_existent_file.o3d");

        // Act
        var result = await reader.ReadAsync(filePath);

        // Assert
        Assert.Equal(O3dMetadataStatus.Failed, result.Status);
        Assert.Null(result.Metadata);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.ReadFailed, result.Diagnostics[0].Code);
        Assert.Equal(O3dDiagnosticSeverity.Error, result.Diagnostics[0].Severity);
    }

    [Fact]
    public async Task O3dMetadataReader_ShouldNotReadGeometryPayload_AfterMetadataSection()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"O3dGeomAudit_{Guid.NewGuid():N}.o3d");
        
        // Version 3 header with:
        // meshCount = 1, vertexCount = 1000 (demands 32KB if read), triangleCount = 500, materialCount = 0
        // Total bytes written is only the header (20 bytes). No geometry bytes are present.
        byte[] headerBytes = [
            0x03, 0x00, 0x00, 0x00, // Version 3
            0x01, 0x00, 0x00, 0x00, // Meshes
            0xe8, 0x03, 0x00, 0x00, // Vertices (1000)
            0xf4, 0x01, 0x00, 0x00, // Triangles (500)
            0x00, 0x00, 0x00, 0x00  // Materials (0)
        ];
        await File.WriteAllBytesAsync(tempFile, headerBytes);

        try
        {
            // Act
            var result = await reader.ReadAsync(tempFile);

            // Assert
            Assert.Equal(O3dMetadataStatus.Success, result.Status);
            Assert.NotNull(result.Metadata);
            Assert.Equal(1000, result.Metadata.VertexCount);
            Assert.Equal(500, result.Metadata.TriangleCount);
            // It did not crash due to missing geometry bytes!
            Assert.Empty(result.Diagnostics);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData(100001, 10, 10, 1)] // meshes limit exceeded
    [InlineData(1, 1000001, 10, 1)] // vertices limit exceeded
    [InlineData(1, 10, 1000001, 1)] // triangles limit exceeded
    [InlineData(1, 10, 10, 100001)] // materials limit exceeded
    public async Task O3dMetadataReader_ShouldRejectAllExcessiveCountFields(uint meshes, uint vertices, uint triangles, uint materials)
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"O3dCountAudit_{Guid.NewGuid():N}.o3d");
        
        byte[] fileBytes = new byte[20];
        Buffer.BlockCopy(BitConverter.GetBytes((uint)3), 0, fileBytes, 0, 4); // version
        Buffer.BlockCopy(BitConverter.GetBytes(meshes), 0, fileBytes, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(vertices), 0, fileBytes, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(triangles), 0, fileBytes, 12, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(materials), 0, fileBytes, 16, 4);

        await File.WriteAllBytesAsync(tempFile, fileBytes);

        try
        {
            // Act
            var result = await reader.ReadAsync(tempFile);

            // Assert
            Assert.Equal(O3dMetadataStatus.Invalid, result.Status);
            Assert.Null(result.Metadata);
            Assert.Single(result.Diagnostics);
            Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, result.Diagnostics[0].Code);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData(new byte[] { 0x02, 0x00 })] // Legacy version, counts truncated
    [InlineData(new byte[] { 0x03, 0x00 })] // V3 version, second word truncated
    [InlineData(new byte[] { 0x03, 0x00, 0x00, 0x00 })] // V3 version, count fields truncated
    public async Task O3dMetadataReader_ShouldReturnControlledDiagnostics_ForTruncatedCountFields(byte[] bytes)
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"O3dTruncCountAudit_{Guid.NewGuid():N}.o3d");
        
        // Write incomplete file
        await File.WriteAllBytesAsync(tempFile, bytes);

        try
        {
            // Act
            var result = await reader.ReadAsync(tempFile);

            // Assert
            Assert.Equal(O3dMetadataStatus.Invalid, result.Status);
            Assert.Single(result.Diagnostics);
            Assert.Equal(O3dDiagnosticCode.TruncatedStream, result.Diagnostics[0].Code);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task O3dMetadataReader_ShouldReturnControlledDiagnostics_ForTruncatedStringPrefix()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"O3dTruncStrAudit_{Guid.NewGuid():N}.o3d");
        
        // Version 3, materials = 1, but we truncate the file right after the header (no 4 bytes for material string length)
        byte[] fileBytes = [
            0x03, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x64, 0x00, 0x00, 0x00,
            0x32, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00
        ];
        await File.WriteAllBytesAsync(tempFile, fileBytes);

        try
        {
            // Act
            var result = await reader.ReadAsync(tempFile);

            // Assert
            Assert.Equal(O3dMetadataStatus.Invalid, result.Status);
            Assert.Single(result.Diagnostics);
            Assert.Equal(O3dDiagnosticCode.TruncatedStream, result.Diagnostics[0].Code);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task O3dMetadataReader_ShouldRespectPreCancelledTokenBeforeTextureLoop()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"O3dCancelLoopAudit_{Guid.NewGuid():N}.o3d");
        
        // Header with 10 materials. We will cancel the operation via cancellation token.
        // Even if we provide the data, a cancellation request should stop processing.
        byte[] headerBytes = [
            0x03, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x64, 0x00, 0x00, 0x00,
            0x32, 0x00, 0x00, 0x00,
            0x0a, 0x00, 0x00, 0x00  // 10 materials
        ];
        await File.WriteAllBytesAsync(tempFile, headerBytes);

        using var cts = new CancellationTokenSource();

        try
        {
            // Act & Assert
            // We cancel it immediately before calling ReadAsync to verify pre-cancel behavior.
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await reader.ReadAsync(tempFile, cts.Token)
            );
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_WithRealO3dHeader_SucceedsAndReturnsExpectedCounts()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"O3dRealTest_{Guid.NewGuid():N}.o3d");
        
        using (var ms = new MemoryStream())
        {
            // Magic
            ms.WriteByte(0x84);
            ms.WriteByte(0x19);
            // Version
            ms.WriteByte(0x07);
            // Options
            ms.WriteByte(0x00);
            // Encryption Key
            ms.Write([0xff, 0xff, 0xff, 0xff], 0, 4);
            
            // Vertices section
            ms.WriteByte(0x17);
            ms.Write([0x64, 0x00, 0x00, 0x00], 0, 4); // 100 vertices
            ms.Write(new byte[100 * 32], 0, 100 * 32);
            
            // Triangles section
            ms.WriteByte(0x49);
            ms.Write([0x32, 0x00, 0x00, 0x00], 0, 4); // 50 triangles
            ms.Write(new byte[50 * 8], 0, 50 * 8); // short indices
            
            // Materials section
            ms.WriteByte(0x26);
            ms.Write([0x01, 0x00], 0, 2); // 1 material
            ms.Write(new byte[44], 0, 44);
            ms.WriteByte(0x0b); // length 11
            ms.Write(System.Text.Encoding.ASCII.GetBytes("texture.bmp"), 0, 11);
            
            await File.WriteAllBytesAsync(tempFile, ms.ToArray());
        }
        
        try
        {
            // Act
            var result = await reader.ReadAsync(tempFile);
            
            // Assert
            Assert.Equal(O3dMetadataStatus.Success, result.Status);
            Assert.NotNull(result.Metadata);
            Assert.Equal(O3dFormatVersion.Version3, result.Metadata.Version);
            Assert.Equal(7, result.Metadata.RawVersion);
            Assert.Equal("7", result.Metadata.DisplayVersion);
            Assert.False(result.Metadata.IsEncrypted);
            Assert.Equal(1, result.Metadata.MeshCount);
            Assert.Equal(100, result.Metadata.VertexCount);
            Assert.Equal(50, result.Metadata.TriangleCount);
            Assert.Equal(1, result.Metadata.MaterialCount);
            Assert.Single(result.Metadata.TextureReferences);
            Assert.Equal("texture.bmp", result.Metadata.TextureReferences[0].Path);
            Assert.Empty(result.Diagnostics);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_WithRealO3dLegacyHeader_SucceedsAndReturnsExpectedCounts()
    {
        // Arrange
        var reader = new O3dMetadataReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"O3dRealLegacyTest_{Guid.NewGuid():N}.o3d");
        
        using (var ms = new MemoryStream())
        {
            // Magic
            ms.WriteByte(0x84);
            ms.WriteByte(0x19);
            // Version
            ms.WriteByte(0x02); // Legacy version
            
            // Vertices section
            ms.WriteByte(0x17);
            ms.Write([0x64, 0x00], 0, 2); // 100 vertices as ushort
            ms.Write(new byte[100 * 32], 0, 100 * 32);
            
            // Triangles section
            ms.WriteByte(0x49);
            ms.Write([0x32, 0x00], 0, 2); // 50 triangles as ushort
            ms.Write(new byte[50 * 8], 0, 50 * 8); // short indices
            
            // Materials section
            ms.WriteByte(0x26);
            ms.Write([0x01, 0x00], 0, 2); // 1 material
            ms.Write(new byte[44], 0, 44);
            ms.WriteByte(0x0b); // length 11
            ms.Write(System.Text.Encoding.ASCII.GetBytes("texture.bmp"), 0, 11);
            
            await File.WriteAllBytesAsync(tempFile, ms.ToArray());
        }
        
        try
        {
            // Act
            var result = await reader.ReadAsync(tempFile);
            
            // Assert
            Assert.Equal(O3dMetadataStatus.Success, result.Status);
            Assert.NotNull(result.Metadata);
            Assert.Equal(O3dFormatVersion.Legacy, result.Metadata.Version);
            Assert.Equal(2, result.Metadata.RawVersion);
            Assert.Equal("2", result.Metadata.DisplayVersion);
            Assert.False(result.Metadata.IsEncrypted);
            Assert.Equal(1, result.Metadata.MeshCount);
            Assert.Equal(100, result.Metadata.VertexCount);
            Assert.Equal(50, result.Metadata.TriangleCount);
            Assert.Equal(1, result.Metadata.MaterialCount);
            Assert.Single(result.Metadata.TextureReferences);
            Assert.Equal("texture.bmp", result.Metadata.TextureReferences[0].Path);
            Assert.Empty(result.Diagnostics);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
