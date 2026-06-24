using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using OmsiStudio.OmsiFormat.Sco;

namespace OmsiStudio.OmsiFormat.Tests;

public sealed class ScoParserFixtureTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ScoParser _parser = new();

    public ScoParserFixtureTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _tempDirectory = Path.Combine(AppContext.BaseDirectory, "FixtureTemp_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_tempDirectory))
        {
            Directory.CreateDirectory(_tempDirectory);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Ignore clean up errors
        }
    }

    private string GetFixturePath(string filename)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sco", filename);
    }

    [Fact]
    public void ParseFile_BasicObjectFixture_ShouldExtractCorrectMetadata()
    {
        // Arrange
        var path = GetFixturePath("basic_object.sco");

        // Act
        var result = _parser.ParseFile(path);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Basic House", result.FriendlyName);
        Assert.Equal("A simple house for testing core parser functionality.", result.Description);
        Assert.Equal(2, result.Groups.Count);
        Assert.Equal("Synthetic Buildings", result.Groups[0]);
        Assert.Equal("Residential", result.Groups[1]);
        Assert.Single(result.Meshes);
        Assert.Equal("basic_house_model.o3d", result.Meshes[0].MeshPath);
        Assert.Equal(2, result.TextureReferences.Count);
        Assert.Contains("house_texture_diffuse.bmp", result.TextureReferences);
        Assert.Contains("house_texture_specular.tga", result.TextureReferences);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ParseFile_ComplexReferencesFixture_ShouldExtractCorrectMetadata()
    {
        // Arrange
        var path = GetFixturePath("complex_references.sco");

        // Act
        var result = _parser.ParseFile(path);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Animated Traffic Light", result.FriendlyName);
        Assert.Equal("An interactive traffic light model with animation scripts, sound triggers, and collision meshes.", result.Description);
        Assert.Equal(3, result.Groups.Count);
        Assert.Equal("Traffic Control", result.Groups[0]);
        Assert.Equal("Signals", result.Groups[1]);
        Assert.Equal("Crossing", result.Groups[2]);
        Assert.Equal(2, result.Meshes.Count);
        Assert.Equal("base_post.o3d", result.Meshes[0].MeshPath);
        Assert.Equal("light_head.o3d", result.Meshes[1].MeshPath);
        Assert.Single(result.ScriptReferences);
        Assert.Equal(@"scripts\traffic_light_logic.osc", result.ScriptReferences[0]);
        Assert.Single(result.SoundReferences);
        Assert.Equal(@"sound\relay_click.cfg", result.SoundReferences[0]);
        Assert.Single(result.CollisionMeshReferences);
        Assert.Equal(@"collision\post_collider.o3d", result.CollisionMeshReferences[0]);
        Assert.False(result.IsNoCollision);
        Assert.True(result.IsFixed);
        Assert.Equal(2, result.TextureReferences.Count);
        Assert.Contains("light_reflector.png", result.TextureReferences);
        Assert.Contains("traffic_glow.dds", result.TextureReferences);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ParseFile_MalformedGroupsFixture_ShouldHaveWarnings()
    {
        // Arrange
        var path = GetFixturePath("malformed_groups.sco");

        // Act
        var result = _parser.ParseFile(path);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Malformed Groups Object", result.FriendlyName);
        Assert.Single(result.Groups);
        Assert.Equal("Only Group One", result.Groups[0]);
        Assert.Single(result.Warnings);
        Assert.Contains("Groups block declared fewer entries than count specified.", result.Warnings[0]);
    }

    [Fact]
    public void ParseFile_LocalizedTurkishWin1254_ShouldParseCorrectlyViaFallback()
    {
        // Arrange
        // NOTE: The repository and text editors store .sco source files in UTF-8.
        // To guarantee testing of legacy Turkish character parsing under the Windows-1254 encoding,
        // we read the UTF-8 template fixture, convert its content to Windows-1254 bytes,
        // write those bytes to a temp file, and parse it.
        var templatePath = GetFixturePath("localized_turkish_win1254.sco");
        var content = File.ReadAllText(templatePath, Encoding.UTF8);

        var win1254 = Encoding.GetEncoding(1254);
        var tempFilePath = Path.Combine(_tempDirectory, "localized_turkish_win1254_actual.sco");
        File.WriteAllText(tempFilePath, content, win1254);

        // Act
        var result = _parser.ParseFile(tempFilePath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Tarihi Kapı ve Yaya Geçidi", result.FriendlyName);
        Assert.Equal("Türkçe karakterler içeren (Ç, Ş, Ğ, İ, ı, ö, ü) tarihi kapı modeli.", result.Description);
        Assert.Equal(2, result.Groups.Count);
        Assert.Equal("Şehir Dekorları", result.Groups[0]);
        Assert.Equal("Geçitler & Kapılar", result.Groups[1]);
        Assert.Single(result.Meshes);
        Assert.Equal("kapi_modeli.o3d", result.Meshes[0].MeshPath);
        Assert.Single(result.TextureReferences);
        Assert.Equal("kaplama_dokusu.png", result.TextureReferences[0]);
        Assert.Empty(result.Warnings);
    }
}
