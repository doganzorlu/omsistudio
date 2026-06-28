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

    [Fact]
    public void Parse_ShouldExtractExpandedMetadata_ScriptsSoundsCollisionsAndFlags()
    {
        // Arrange
        var lines = new[]
        {
            "' General comments",
            "[friendlyname]",
            "Expanded Test Object",
            "",
            "[script]",
            "scripts\\my_script.osc",
            "[script]",
            "scripts\\another_script.osc",
            "",
            "[sound]",
            "sound\\my_sound.cfg",
            "",
            "[collision_mesh]",
            "collision\\box.o3d",
            "",
            "[nocollision]",
            "[fixed]"
        };
        var parser = new ScoParser();

        // Act
        var result = parser.Parse("test.sco", lines);

        // Assert
        Assert.Equal("Expanded Test Object", result.FriendlyName);
        Assert.Equal(2, result.ScriptReferences.Count);
        Assert.Equal("scripts\\my_script.osc", result.ScriptReferences[0]);
        Assert.Equal("scripts\\another_script.osc", result.ScriptReferences[1]);
        Assert.Single(result.SoundReferences);
        Assert.Equal("sound\\my_sound.cfg", result.SoundReferences[0]);
        Assert.Single(result.CollisionMeshReferences);
        Assert.Equal("collision\\box.o3d", result.CollisionMeshReferences[0]);
        Assert.True(result.IsNoCollision);
        Assert.True(result.IsFixed);
    }

    [Fact]
    public void Parse_ShouldExtractMeshTransforms_WhenValid()
    {
        // Arrange
        var lines = new[]
        {
            "[mesh]",
            "model.o3d",
            "[new_pos]",
            "1.2",
            "-3.4",
            "5.6",
            "[rot_x]",
            "45",
            "[rot_y]",
            "-90.5",
            "[rot_z]",
            "180",
            "[scale]",
            "0.5",
            "1.5",
            "2.0"
        };
        var parser = new ScoParser();

        // Act
        var result = parser.Parse("test.sco", lines);

        // Assert
        Assert.Single(result.Meshes);
        var mesh = result.Meshes[0];
        Assert.Equal("model.o3d", mesh.MeshPath);
        Assert.Equal(1.2, mesh.PosX);
        Assert.Equal(-3.4, mesh.PosY);
        Assert.Equal(5.6, mesh.PosZ);
        Assert.Equal(45.0, mesh.RotX);
        Assert.Equal(-90.5, mesh.RotY);
        Assert.Equal(180.0, mesh.RotZ);
        Assert.Equal(0.5, mesh.ScaleX);
        Assert.Equal(1.5, mesh.ScaleY);
        Assert.Equal(2.0, mesh.ScaleZ);
        Assert.Empty(result.Warnings);
        Assert.Empty(mesh.Warnings);
    }

    [Fact]
    public void Parse_ShouldFallbackToIdentityAndWarn_WhenTransformIsMalformed()
    {
        // Arrange
        var lines = new[]
        {
            "[mesh]",
            "model.o3d",
            "[new_pos]",
            "1.2",
            "invalid_y",
            "5.6",
            "[rot_x]",
            "invalid_angle",
            "[scale]",
            "invalid_scale"
        };
        var parser = new ScoParser();

        // Act
        var result = parser.Parse("test.sco", lines);

        // Assert
        Assert.Single(result.Meshes);
        var mesh = result.Meshes[0];
        Assert.Equal(0.0, mesh.PosX); // Default/fallback
        Assert.Equal(0.0, mesh.PosY);
        Assert.Equal(0.0, mesh.PosZ);
        Assert.Equal(0.0, mesh.RotX);
        Assert.Equal(1.0, mesh.ScaleX);
        Assert.NotEmpty(result.Warnings);
        Assert.NotEmpty(mesh.Warnings);
    }
}
