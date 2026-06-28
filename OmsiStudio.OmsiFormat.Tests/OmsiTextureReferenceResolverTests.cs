using System;
using System.IO;
using OmsiStudio.Core.Assets;
using OmsiStudio.OmsiFormat.Scanner;
using Xunit;

namespace OmsiStudio.OmsiFormat.Tests;

public class OmsiTextureReferenceResolverTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _sceneryObjectsRoot;
    private readonly string _assetDir;
    private readonly string _modelDir;
    private readonly string _modelFile;

    public OmsiTextureReferenceResolverTests()
    {
        // Setup isolated temp directory structure
        _testRoot = Path.Combine(Path.GetTempPath(), "OmsiTextureResolverTests_" + Guid.NewGuid().ToString("N"));
        _sceneryObjectsRoot = Path.Combine(_testRoot, "Sceneryobjects");
        _assetDir = Path.Combine(_sceneryObjectsRoot, "Berlin");
        _modelDir = Path.Combine(_assetDir, "model");
        _modelFile = Path.Combine(_modelDir, "house.o3d");

        Directory.CreateDirectory(_testRoot);
        Directory.CreateDirectory(_sceneryObjectsRoot);
        Directory.CreateDirectory(_assetDir);
        Directory.CreateDirectory(_modelDir);
        
        // Create dummy o3d file path (file content not needed for resolver)
        File.WriteAllText(_modelFile, "dummy o3d");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }
        catch
        {
            // Suppress cleanup exception
        }
    }

    [Fact]
    public void Resolve_DirectModelFolder_ResolvesSuccessfully()
    {
        // Arrange
        var targetFile = Path.Combine(_modelDir, "brick.bmp");
        File.WriteAllText(targetFile, "dummy bmp");

        var resolver = new OmsiTextureReferenceResolver();

        // Act
        var result = resolver.Resolve("brick.bmp", _modelFile, _sceneryObjectsRoot);

        // Assert
        Assert.Equal(OmsiTextureReferenceResolutionStatus.Resolved, result.ResolutionStatus);
        Assert.Equal(targetFile, result.ResolvedPath);
        Assert.True(result.Exists);
    }

    [Fact]
    public void Resolve_TextureSubfolder_ResolvesSuccessfully()
    {
        // Arrange
        var texDir = Path.Combine(_modelDir, "texture");
        Directory.CreateDirectory(texDir);
        var targetFile = Path.Combine(texDir, "brick.bmp");
        File.WriteAllText(targetFile, "dummy bmp");

        var resolver = new OmsiTextureReferenceResolver();

        // Act
        var result = resolver.Resolve("brick.bmp", _modelFile, _sceneryObjectsRoot);

        // Assert
        Assert.Equal(OmsiTextureReferenceResolutionStatus.Resolved, result.ResolutionStatus);
        Assert.Equal(targetFile, result.ResolvedPath);
    }

    [Fact]
    public void Resolve_AssetFolder_ResolvesSuccessfully()
    {
        // Arrange
        var targetFile = Path.Combine(_assetDir, "facade.bmp");
        File.WriteAllText(targetFile, "dummy bmp");

        var resolver = new OmsiTextureReferenceResolver();

        // Act
        var result = resolver.Resolve("facade.bmp", _modelFile, _sceneryObjectsRoot);

        // Assert
        Assert.Equal(OmsiTextureReferenceResolutionStatus.Resolved, result.ResolutionStatus);
        Assert.Equal(targetFile, result.ResolvedPath);
    }

    [Fact]
    public void Resolve_RootRelative_ResolvesSuccessfully()
    {
        // Arrange
        // Sceneryobjects/Common/textures/glass.bmp
        var commonDir = Path.Combine(_sceneryObjectsRoot, "Common", "textures");
        Directory.CreateDirectory(commonDir);
        var targetFile = Path.Combine(commonDir, "glass.bmp");
        File.WriteAllText(targetFile, "dummy bmp");

        var resolver = new OmsiTextureReferenceResolver();

        // Act
        var result = resolver.Resolve("Common/textures/glass.bmp", _modelFile, _sceneryObjectsRoot);

        // Assert
        Assert.Equal(OmsiTextureReferenceResolutionStatus.Resolved, result.ResolutionStatus);
        Assert.Equal(targetFile, result.ResolvedPath);
    }

    [Fact]
    public void Resolve_CaseInsensitive_ResolvesSuccessfully()
    {
        // Arrange
        var targetFile = Path.Combine(_modelDir, "BRICK.BMP");
        File.WriteAllText(targetFile, "dummy bmp");

        var resolver = new OmsiTextureReferenceResolver();

        // Act
        var result = resolver.Resolve("brick.bmp", _modelFile, _sceneryObjectsRoot);

        // Assert
        Assert.Equal(OmsiTextureReferenceResolutionStatus.Resolved, result.ResolutionStatus);
        Assert.Equal(targetFile, result.ResolvedPath);
    }

    [Fact]
    public void Resolve_MissingFile_ReturnsMissingStatus()
    {
        // Arrange
        var resolver = new OmsiTextureReferenceResolver();

        // Act
        var result = resolver.Resolve("missing.bmp", _modelFile, _sceneryObjectsRoot);

        // Assert
        Assert.Equal(OmsiTextureReferenceResolutionStatus.Missing, result.ResolutionStatus);
        Assert.False(result.Exists);
        Assert.NotNull(result.ResolvedPath);
    }

    [Fact]
    public void Resolve_TraversalOutsideRoot_ReturnsInvalidPath()
    {
        // Arrange
        var resolver = new OmsiTextureReferenceResolver();
        // Trying to go outside Sceneryobjects root using ../../../../
        var traversalPath = "../../../../escaped.bmp";

        // Act
        var result = resolver.Resolve(traversalPath, _modelFile, _sceneryObjectsRoot);

        // Assert
        Assert.Equal(OmsiTextureReferenceResolutionStatus.InvalidPath, result.ResolutionStatus);
        Assert.Null(result.ResolvedPath);
    }
    [Fact]
    public void Resolve_RelativePathWithTraversalSegment_ReturnsInvalidPath_EvenIfFileExists()
    {
        // Arrange
        // Put facade.bmp in the asset folder
        var targetFile = Path.Combine(_assetDir, "facade.bmp");
        File.WriteAllText(targetFile, "dummy bmp");

        var resolver = new OmsiTextureReferenceResolver();

        // Act 1: ../facade.bmp
        var result1 = resolver.Resolve("../facade.bmp", _modelFile, _sceneryObjectsRoot);

        // Act 2: texture/../facade.bmp
        var result2 = resolver.Resolve("texture/../facade.bmp", _modelFile, _sceneryObjectsRoot);

        // Act 3: texture\\..\\facade.bmp
        var result3 = resolver.Resolve(@"texture\..\facade.bmp", _modelFile, _sceneryObjectsRoot);

        // Assert
        Assert.Equal(OmsiTextureReferenceResolutionStatus.InvalidPath, result1.ResolutionStatus);
        Assert.Null(result1.ResolvedPath);

        Assert.Equal(OmsiTextureReferenceResolutionStatus.InvalidPath, result2.ResolutionStatus);
        Assert.Null(result2.ResolvedPath);

        Assert.Equal(OmsiTextureReferenceResolutionStatus.InvalidPath, result3.ResolutionStatus);
        Assert.Null(result3.ResolvedPath);
    }

    [Fact]
    public void Resolve_SiblingPathEdgeCase_DoesNotProduceFalsePositives()
    {
        // Arrange
        // Create sibling directory to scenery objects root, e.g. Sceneryobjects-sibling
        var siblingDir = _sceneryObjectsRoot + "-sibling";
        Directory.CreateDirectory(siblingDir);
        var targetFile = Path.Combine(siblingDir, "brick.bmp");
        File.WriteAllText(targetFile, "dummy bmp");

        var resolver = new OmsiTextureReferenceResolver();

        // Act
        var result = resolver.Resolve(targetFile, _modelFile, _sceneryObjectsRoot);

        // Assert
        // Since the absolute path is in a sibling directory (not descendant), it must be InvalidPath, not resolved.
        Assert.Equal(OmsiTextureReferenceResolutionStatus.InvalidPath, result.ResolutionStatus);
    }
    [Fact]
    public void Resolve_AbsolutePathInsideAllowedRoot_ResolvesSuccessfully()
    {
        // Arrange
        var targetFile = Path.Combine(_modelDir, "brick.bmp");
        File.WriteAllText(targetFile, "dummy bmp");

        var resolver = new OmsiTextureReferenceResolver();

        // Act
        var result = resolver.Resolve(targetFile, _modelFile, _sceneryObjectsRoot);

        // Assert
        Assert.Equal(OmsiTextureReferenceResolutionStatus.Resolved, result.ResolutionStatus);
        Assert.Equal(targetFile, result.ResolvedPath);
    }

    [Fact]
    public void Resolve_AbsolutePathOutsideAllowedRoot_ReturnsInvalidPath()
    {
        // Arrange
        var tempOutside = Path.Combine(Path.GetTempPath(), "outside_" + Guid.NewGuid().ToString("N") + ".bmp");
        File.WriteAllText(tempOutside, "dummy outside");

        try
        {
            var resolver = new OmsiTextureReferenceResolver();

            // Act
            var result = resolver.Resolve(tempOutside, _modelFile, _sceneryObjectsRoot);

            // Assert
            Assert.Equal(OmsiTextureReferenceResolutionStatus.InvalidPath, result.ResolutionStatus);
        }
        finally
        {
            if (File.Exists(tempOutside))
            {
                File.Delete(tempOutside);
            }
        }
    }
}
