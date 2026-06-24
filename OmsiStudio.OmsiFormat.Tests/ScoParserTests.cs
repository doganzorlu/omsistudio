using System.Collections.Generic;
using Xunit;
using OmsiStudio.OmsiFormat.Sco;

namespace OmsiStudio.OmsiFormat.Tests;

public class ScoParserTests
{
    [Fact]
    public void Parse_ShouldExtractFriendlyName_AndIgnoreCommentsAndEmptyLines()
    {
        // Arrange
        var lines = new[]
        {
            "' This is a starting comment line",
            "",
            "[friendlyname]",
            "   My Scenery Object   ",
            "",
            "' Another comment",
            "[description]",
            "Some description"
        };
        var parser = new ScoParser();

        // Act
        var result = parser.Parse("test.sco", lines);

        // Assert
        Assert.Equal("My Scenery Object", result.FriendlyName);
        Assert.Equal("Some description", result.Description);
    }

    [Fact]
    public void Parse_ShouldExtractMultipleMeshes()
    {
        // Arrange
        var lines = new[]
        {
            "[mesh]",
            "model1.o3d",
            "[mesh]",
            "model2.o3d"
        };
        var parser = new ScoParser();

        // Act
        var result = parser.Parse("test.sco", lines);

        // Assert
        Assert.Equal(2, result.Meshes.Count);
        Assert.Equal("model1.o3d", result.Meshes[0].MeshPath);
        Assert.Equal("model2.o3d", result.Meshes[1].MeshPath);
    }

    [Fact]
    public void Parse_ShouldHandleCaseVariationInTokens()
    {
        // Arrange
        var lines = new[]
        {
            "[FRIENDLYNAME]",
            "Test Upper",
            "[MeSh]",
            "model.o3d"
        };
        var parser = new ScoParser();

        // Act
        var result = parser.Parse("test.sco", lines);

        // Assert
        Assert.Equal("Test Upper", result.FriendlyName);
        Assert.Equal("model.o3d", result.Meshes[0].MeshPath);
    }

    [Fact]
    public void Parse_ShouldExtractObviousTextureReferences_FromScoContent_CaseInsensitiveDeduplicated()
    {
        // Arrange
        var lines = new[]
        {
            "some_texture.bmp",
            "[texture]",
            "another_texture.dds",
            "ignore_this_line",
            "builder's_detail.png",
            "duplicate.tga",
            "DUPLICATE.TGA"
        };
        var parser = new ScoParser();

        // Act
        var result = parser.Parse("test.sco", lines);

        // Assert
        Assert.Equal(4, result.TextureReferences.Count);
        Assert.Contains("some_texture.bmp", result.TextureReferences);
        Assert.Contains("another_texture.dds", result.TextureReferences);
        Assert.Contains("builder's_detail.png", result.TextureReferences);
        Assert.Contains("duplicate.tga", result.TextureReferences);
    }

    [Fact]
    public void Parse_ShouldTolerateMalformedAndEmptyFiles()
    {
        // Arrange
        var lines = new string[] { };
        var parser = new ScoParser();

        // Act
        var result = parser.Parse("test.sco", lines);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.FriendlyName);
        Assert.Empty(result.Meshes);
        Assert.Empty(result.TextureReferences);
    }

    [Fact]
    public void Parse_ShouldPreserveApostrophesInValues_AndIgnoreFullLineComments()
    {
        // Arrange
        var lines = new[]
        {
            "' This is a full-line comment that should be ignored",
            "[friendlyname]",
            "Driver's Hut",
            "[mesh]",
            "models\\builder's_wall.o3d"
        };
        var parser = new ScoParser();

        // Act
        var result = parser.Parse("test.sco", lines);

        // Assert
        Assert.Equal("Driver's Hut", result.FriendlyName);
        Assert.Single(result.Meshes);
        Assert.Equal("models\\builder's_wall.o3d", result.Meshes[0].MeshPath);
    }
}
