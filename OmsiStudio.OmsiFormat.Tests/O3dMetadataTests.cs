using System;
using System.Collections.Generic;
using Xunit;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.OmsiFormat.Tests;

public class O3dMetadataTests
{
    [Fact]
    public void O3dEnums_ShouldDefaultToUnknownZero()
    {
        // Assert
        Assert.Equal(0, (int)O3dFormatVersion.Unknown);
        Assert.Equal(0, (int)O3dMetadataStatus.Unknown);

        Assert.Equal(O3dFormatVersion.Unknown, default(O3dFormatVersion));
        Assert.Equal(O3dMetadataStatus.Unknown, default(O3dMetadataStatus));
    }

    [Fact]
    public void O3dMetadata_PropertiesShouldInitializeCorrectly()
    {
        // Arrange & Act
        var metadata = new O3dMetadata
        {
            Version = O3dFormatVersion.Version3,
            RawVersion = 7,
            IsEncrypted = false,
            MeshCount = 1,
            VertexCount = 120,
            TriangleCount = 80,
            MaterialCount = 2,
            TextureReferences = new List<O3dTextureReference>
            {
                new() { Path = "texture1.bmp" },
                new() { Path = "texture2.bmp" }
            }
        };

        // Assert
        Assert.Equal(O3dFormatVersion.Version3, metadata.Version);
        Assert.Equal(7, metadata.RawVersion);
        Assert.Equal("7", metadata.DisplayVersion);
        Assert.False(metadata.IsEncrypted);
        Assert.Equal(1, metadata.MeshCount);
        Assert.Equal(120, metadata.VertexCount);
        Assert.Equal(80, metadata.TriangleCount);
        Assert.Equal(2, metadata.MaterialCount);
        Assert.Equal(2, metadata.TextureReferences.Count);
        Assert.Equal("texture1.bmp", metadata.TextureReferences[0].Path);
        Assert.Equal("texture2.bmp", metadata.TextureReferences[1].Path);
    }

    [Fact]
    public void O3dMetadataReadResult_PropertiesShouldInitializeCorrectly()
    {
        // Arrange
        var metadata = new O3dMetadata
        {
            Version = O3dFormatVersion.Legacy,
            IsEncrypted = true
        };

        var diagnostics = new List<O3dDiagnostic>
        {
            new() { Severity = O3dDiagnosticSeverity.Info, Code = O3dDiagnosticCode.Unknown, Message = "Header successfully parsed." },
            new() { Severity = O3dDiagnosticSeverity.Error, Code = O3dDiagnosticCode.EncryptedFile, Message = "Encryption detected.", ByteOffset = 4 }
        };

        // Act
        var result = new O3dMetadataReadResult
        {
            Metadata = metadata,
            Status = O3dMetadataStatus.Encrypted,
            Diagnostics = diagnostics
        };

        // Assert
        Assert.NotNull(result.Metadata);
        Assert.Equal(O3dFormatVersion.Legacy, result.Metadata.Version);
        Assert.Equal("Legacy", result.Metadata.DisplayVersion);
        Assert.True(result.Metadata.IsEncrypted);
        Assert.Equal(O3dMetadataStatus.Encrypted, result.Status);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal(O3dDiagnosticSeverity.Error, result.Diagnostics[1].Severity);
        Assert.Equal(O3dDiagnosticCode.EncryptedFile, result.Diagnostics[1].Code);
        Assert.Equal(4, result.Diagnostics[1].ByteOffset);
    }

    [Fact]
    public void O3dDiagnostic_ShouldDefaultToUnknownAndEmpty()
    {
        // Arrange & Act
        var diagnostic = new O3dDiagnostic();

        // Assert
        Assert.Equal(O3dDiagnosticSeverity.Unknown, diagnostic.Severity);
        Assert.Equal(O3dDiagnosticCode.Unknown, diagnostic.Code);
        Assert.Equal(string.Empty, diagnostic.Message);
        Assert.Null(diagnostic.ByteOffset);
        Assert.Null(diagnostic.Context);
    }
}
