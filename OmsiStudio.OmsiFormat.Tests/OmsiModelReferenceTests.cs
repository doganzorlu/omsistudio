using System;
using System.Collections.Generic;
using Xunit;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.OmsiFormat.Tests;

public class OmsiModelReferenceTests
{
    [Fact]
    public void DefaultConstructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var modelRef = new OmsiModelReference();

        // Assert
        Assert.Equal(string.Empty, modelRef.MeshPath);
        Assert.Equal(string.Empty, modelRef.ResolvedPath);
        Assert.False(modelRef.Exists);
        Assert.Equal(OmsiModelReferenceResolutionStatus.Unknown, modelRef.ResolutionStatus);
        
        Assert.Null(modelRef.Metadata);
        Assert.Equal(O3dMetadataStatus.Unknown, modelRef.MetadataStatus);
        Assert.Empty(modelRef.MetadataDiagnostics);
        
        Assert.False(modelRef.HasMetadata);
        Assert.True(modelRef.HasNoMetadata);
        Assert.False(modelRef.HasMetadataDiagnostics);
    }

    [Fact]
    public void MeshPathConstructor_ShouldSetMeshPath()
    {
        // Arrange
        var path = "mesh.o3d";

        // Act
        var modelRef = new OmsiModelReference(path);

        // Assert
        Assert.Equal(path, modelRef.MeshPath);
        Assert.Equal(string.Empty, modelRef.ResolvedPath);
        Assert.False(modelRef.Exists);
        Assert.Equal(OmsiModelReferenceResolutionStatus.Unknown, modelRef.ResolutionStatus);
    }

    [Fact]
    public void FullConstructor_ShouldSetCorrectFields()
    {
        // Act
        var modelRef = new OmsiModelReference("mesh.o3d", "/absolute/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // Assert
        Assert.Equal("mesh.o3d", modelRef.MeshPath);
        Assert.Equal("/absolute/mesh.o3d", modelRef.ResolvedPath);
        Assert.True(modelRef.Exists);
        Assert.Equal(OmsiModelReferenceResolutionStatus.Resolved, modelRef.ResolutionStatus);
    }

    [Fact]
    public void ObjectInitializer_ShouldAllowSettingMetadataAndDiagnostics()
    {
        // Arrange
        var metadata = new O3dMetadata
        {
            Version = O3dFormatVersion.Version3,
            IsEncrypted = false,
            MeshCount = 2,
            VertexCount = 100,
            TriangleCount = 50,
            MaterialCount = 1
        };
        var diagnostics = new List<O3dDiagnostic>
        {
            new() { Severity = O3dDiagnosticSeverity.Error, Code = O3dDiagnosticCode.SafetyLimitExceeded, Message = "Test warning" }
        };

        // Act
        var modelRef = new OmsiModelReference("mesh.o3d", "/absolute/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved)
        {
            Metadata = metadata,
            MetadataStatus = O3dMetadataStatus.Success,
            MetadataDiagnostics = diagnostics
        };

        // Assert
        Assert.NotNull(modelRef.Metadata);
        Assert.Equal(O3dMetadataStatus.Success, modelRef.MetadataStatus);
        Assert.Single(modelRef.MetadataDiagnostics);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, modelRef.MetadataDiagnostics[0].Code);
        
        Assert.True(modelRef.HasMetadata);
        Assert.False(modelRef.HasNoMetadata);
        Assert.True(modelRef.HasMetadataDiagnostics);
    }
}
