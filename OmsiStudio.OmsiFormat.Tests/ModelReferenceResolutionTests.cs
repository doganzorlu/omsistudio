using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Scanning;
using OmsiStudio.Core.Services;
using OmsiStudio.OmsiFormat.Parser;
using OmsiStudio.OmsiFormat.Scanner;

namespace OmsiStudio.OmsiFormat.Tests;

public class ModelReferenceResolutionTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly string _omsiRoot;
    private readonly string _sceneryObjectsDir;

    public ModelReferenceResolutionTests()
    {
        _testTempDir = Path.Combine(AppContext.BaseDirectory, "ModelResTemp_" + Guid.NewGuid().ToString("N"));
        _omsiRoot = Path.Combine(_testTempDir, "OMSI");
        _sceneryObjectsDir = Path.Combine(_omsiRoot, "Sceneryobjects");

        Directory.CreateDirectory(_testTempDir);
        Directory.CreateDirectory(_omsiRoot);
        Directory.CreateDirectory(_sceneryObjectsDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch
        {
            // Ignore clean up failures
        }
    }

    [Fact]
    public void Resolve_ShouldResolveModelPathRelativeToScoFolder()
    {
        // Arrange
        var resolver = new OmsiModelReferenceResolver();
        var authorDir = Path.Combine(_sceneryObjectsDir, "Author1");
        Directory.CreateDirectory(authorDir);

        var scoPath = Path.Combine(authorDir, "object.sco");
        var meshPath = "direct_mesh.o3d";
        var expectedMeshFullPath = Path.Combine(authorDir, meshPath);

        File.WriteAllText(scoPath, "");
        File.WriteAllText(expectedMeshFullPath, "");

        // Act
        var result = resolver.Resolve(_omsiRoot, scoPath, meshPath);

        // Assert
        Assert.Equal(OmsiModelReferenceResolutionStatus.Resolved, result.ResolutionStatus);
        Assert.True(result.Exists);
        Assert.Equal(expectedMeshFullPath, result.ResolvedPath);
    }

    [Fact]
    public void Resolve_ShouldResolveModelPathUnderModelSubfolder()
    {
        // Arrange
        var resolver = new OmsiModelReferenceResolver();
        var authorDir = Path.Combine(_sceneryObjectsDir, "Author2");
        var modelDir = Path.Combine(authorDir, "Model");
        Directory.CreateDirectory(authorDir);
        Directory.CreateDirectory(modelDir);

        var scoPath = Path.Combine(authorDir, "object.sco");
        var meshPath = "sub_mesh.o3d";
        var expectedMeshFullPath = Path.Combine(modelDir, meshPath);

        File.WriteAllText(scoPath, "");
        File.WriteAllText(expectedMeshFullPath, "");

        // Act
        var result = resolver.Resolve(_omsiRoot, scoPath, meshPath);

        // Assert
        Assert.Equal(OmsiModelReferenceResolutionStatus.Resolved, result.ResolutionStatus);
        Assert.True(result.Exists);
        Assert.Equal(expectedMeshFullPath, result.ResolvedPath);
    }

    [Fact]
    public void Resolve_ShouldHandleMixedSlashDirections()
    {
        // Arrange
        var resolver = new OmsiModelReferenceResolver();
        var authorDir = Path.Combine(_sceneryObjectsDir, "Author3");
        var subDir = Path.Combine(authorDir, "SubFolder");
        Directory.CreateDirectory(authorDir);
        Directory.CreateDirectory(subDir);

        var scoPath = Path.Combine(authorDir, "object.sco");
        var meshPath = "SubFolder\\mixed_mesh.o3d";
        var expectedMeshFullPath = Path.Combine(subDir, "mixed_mesh.o3d");

        File.WriteAllText(scoPath, "");
        File.WriteAllText(expectedMeshFullPath, "");

        // Act
        var result = resolver.Resolve(_omsiRoot, scoPath, meshPath);

        // Assert
        Assert.Equal(OmsiModelReferenceResolutionStatus.Resolved, result.ResolutionStatus);
        Assert.True(result.Exists);
        Assert.Equal(expectedMeshFullPath, result.ResolvedPath);
    }

    [Fact]
    public void Resolve_ShouldReturnMissingForNonExistentFile()
    {
        // Arrange
        var resolver = new OmsiModelReferenceResolver();
        var authorDir = Path.Combine(_sceneryObjectsDir, "Author4");
        Directory.CreateDirectory(authorDir);

        var scoPath = Path.Combine(authorDir, "object.sco");
        var meshPath = "does_not_exist.o3d";
        var expectedPathCandidate = Path.Combine(authorDir, meshPath);

        File.WriteAllText(scoPath, "");

        // Act
        var result = resolver.Resolve(_omsiRoot, scoPath, meshPath);

        // Assert
        Assert.Equal(OmsiModelReferenceResolutionStatus.Missing, result.ResolutionStatus);
        Assert.False(result.Exists);
        Assert.Equal(expectedPathCandidate, result.ResolvedPath);
    }

    [Fact]
    public void Resolve_ShouldRejectTraversalOutsideAllowedScope()
    {
        // Arrange
        var resolver = new OmsiModelReferenceResolver();
        var authorDir = Path.Combine(_sceneryObjectsDir, "Author5");
        Directory.CreateDirectory(authorDir);

        var scoPath = Path.Combine(authorDir, "object.sco");
        var meshPath = "../../../../../../outside.o3d"; // Escapes Sceneryobjects and OMSI root

        File.WriteAllText(scoPath, "");

        // Act
        var result = resolver.Resolve(_omsiRoot, scoPath, meshPath);

        // Assert
        Assert.Equal(OmsiModelReferenceResolutionStatus.InvalidPath, result.ResolutionStatus);
        Assert.False(result.Exists);
    }

    [Fact]
    public async Task Scanner_ShouldReportWarningsForMissingAndInvalidReferences()
    {
        // Arrange
        var authorDir = Path.Combine(_sceneryObjectsDir, "Author6");
        Directory.CreateDirectory(authorDir);

        var scoPath = Path.Combine(authorDir, "object.sco");
        var scoContent = @"
[friendlyname]
Test Object

[mesh]
missing_mesh.o3d

[mesh]
../../../../invalid_traversal.o3d
";
        File.WriteAllText(scoPath, scoContent);

        var scanner = new OmsiAssetScanner(new ScoFileParser());

        // Act
        var result = await scanner.ScanAsync(_omsiRoot);

        // Assert
        Assert.Single(result.DiscoveredAssets);
        var asset = result.DiscoveredAssets[0];
        Assert.Equal(2, asset.ModelReferences.Count);

        // Model References check
        Assert.Equal("missing_mesh.o3d", asset.ModelReferences[0].MeshPath);
        Assert.Equal(OmsiModelReferenceResolutionStatus.Missing, asset.ModelReferences[0].ResolutionStatus);

        Assert.Equal("../../../../invalid_traversal.o3d", asset.ModelReferences[1].MeshPath);
        Assert.Equal(OmsiModelReferenceResolutionStatus.InvalidPath, asset.ModelReferences[1].ResolutionStatus);

        // Warnings check
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains("Model reference missing: 'missing_mesh.o3d'", result.Warnings[0]);
        Assert.Contains("Model reference invalid/traversal: '../../../../invalid_traversal.o3d'", result.Warnings[1]);
    }
}
