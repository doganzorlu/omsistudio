using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Conversion;
using OmsiStudio.Conversion;

namespace OmsiStudio.Conversion.Tests;

public class ExportManifestWriterTests : IDisposable
{
    private readonly string _tempDir;

    public ExportManifestWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ExportWriterTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    [Theory]
    [InlineData("valid_name", "/path/to/my_asset.sco", "my_asset_manifest.json")]
    [InlineData("Valid Display Name", "", "Valid Display Name_manifest.json")]
    [InlineData("Invalid*?Name", null, "InvalidName_manifest.json")]
    [InlineData("", "  ", "asset_manifest.json")]
    [InlineData(null, null, "asset_manifest.json")]
    public void GenerateDeterministicFilename_ShouldGenerateExpectedNames(string displayName, string scoPath, string expected)
    {
        // Act
        var filename = ExportManifestWriter.GenerateDeterministicFilename(displayName, scoPath);

        // Assert
        Assert.Equal(expected, filename);
    }

    [Fact]
    public async Task WriteAsync_ShouldWriteJsonFileToDiskAndReturnPath()
    {
        // Arrange
        var serializer = new ExportManifestSerializer();
        var writer = new ExportManifestWriter(serializer);

        var manifest = new ExportManifest
        {
            AssetDisplayName = "Test Asset",
            SourceScoPath = Path.Combine(_tempDir, "test.sco"),
            RelativePath = "Sceneryobjects/test.sco",
            TargetFormat = ConversionTargetFormat.ManifestOnly,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Meshes = new[] { new ExportManifestReference { Path = "mesh1.o3d", Kind = ExportManifestReferenceKind.Mesh } },
            Textures = new[] { new ExportManifestReference { Path = "texture1.png", Kind = ExportManifestReferenceKind.Texture } },
            Warnings = new[] { "A warning" }
        };

        // Act
        var writtenPath = await writer.WriteAsync(manifest, _tempDir);

        // Assert
        Assert.True(File.Exists(writtenPath));
        Assert.Equal(Path.Combine(_tempDir, "test_manifest.json"), writtenPath);

        var jsonContent = await File.ReadAllTextAsync(writtenPath);
        Assert.Contains("\"assetDisplayName\": \"Test Asset\"", jsonContent);
        Assert.Contains("\"path\": \"mesh1.o3d\"", jsonContent);
        Assert.Contains("\"kind\": \"Mesh\"", jsonContent);
        Assert.Contains("\"path\": \"texture1.png\"", jsonContent);
        Assert.Contains("\"kind\": \"Texture\"", jsonContent);
        Assert.Contains("A warning", jsonContent);
    }
}
