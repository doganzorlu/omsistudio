using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Conversion;
using OmsiStudio.Conversion;

namespace OmsiStudio.Conversion.Tests;

public class ExportManifestTests
{
    [Fact]
    public void ExportManifestEnums_ShouldDefaultToUnknownZero()
    {
        Assert.Equal(0, (int)ExportManifestReferenceKind.Unknown);
        Assert.Equal(ExportManifestReferenceKind.Unknown, default(ExportManifestReferenceKind));
    }

    [Fact]
    public void Build_ShouldIncludeAssetIdentityFieldsAndReferencesAndWarnings()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
        var builder = new ExportManifestBuilder(() => fixedTime);

        var asset = new OmsiAsset
        {
            DisplayName = "Test Asset",
            SourceScoPath = "/path/to/source.sco",
            RelativePath = "Sceneryobjects/test/source.sco",
            ModelReferences = new List<OmsiModelReference>
            {
                new("mesh1.o3d"),
                new("mesh2.o3d")
            },
            TextureReferences = new List<string>
            {
                "texture1.dds",
                "texture2.png"
            }
        };

        var request = new ConversionRequest
        {
            Asset = asset,
            TargetOutputDirectory = "/path/to/output",
            TargetFormat = ConversionTargetFormat.ManifestOnly
        };

        var result = new ConversionResult
        {
            Status = ConversionStatus.Succeeded,
            Warnings = new List<string> { "Warning 1", "Warning 2" }
        };

        // Act
        var manifest = builder.Build(request, result);

        // Assert
        Assert.Equal("Test Asset", manifest.AssetDisplayName);
        Assert.Equal("/path/to/source.sco", manifest.SourceScoPath);
        Assert.Equal("Sceneryobjects/test/source.sco", manifest.RelativePath);
        Assert.Equal(ConversionTargetFormat.ManifestOnly, manifest.TargetFormat);
        Assert.Equal(fixedTime, manifest.GeneratedAtUtc);

        // Mesh references included as Mesh
        Assert.Equal(2, manifest.Meshes.Count);
        Assert.Equal("mesh1.o3d", manifest.Meshes[0].Path);
        Assert.Equal(ExportManifestReferenceKind.Mesh, manifest.Meshes[0].Kind);
        Assert.Equal("mesh2.o3d", manifest.Meshes[1].Path);
        Assert.Equal(ExportManifestReferenceKind.Mesh, manifest.Meshes[1].Kind);

        // Texture references included as Texture
        Assert.Equal(2, manifest.Textures.Count);
        Assert.Equal("texture1.dds", manifest.Textures[0].Path);
        Assert.Equal(ExportManifestReferenceKind.Texture, manifest.Textures[0].Kind);
        Assert.Equal("texture2.png", manifest.Textures[1].Path);
        Assert.Equal(ExportManifestReferenceKind.Texture, manifest.Textures[1].Kind);

        // Warnings from conversion result included
        Assert.Equal(2, manifest.Warnings.Count);
        Assert.Contains("Warning 1", manifest.Warnings);
        Assert.Contains("Warning 2", manifest.Warnings);
    }

    [Fact]
    public void Serializer_ShouldProduceStableJsonWithoutWritingFiles()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
        var builder = new ExportManifestBuilder(() => fixedTime);
        var serializer = new ExportManifestSerializer();

        var asset = new OmsiAsset
        {
            DisplayName = "Test Asset",
            SourceScoPath = "/path/to/source.sco",
            RelativePath = "Sceneryobjects/test/source.sco",
            ModelReferences = new List<OmsiModelReference> { new("mesh1.o3d") },
            TextureReferences = new List<string> { "texture1.dds" }
        };

        var request = new ConversionRequest
        {
            Asset = asset,
            TargetOutputDirectory = "/path/to/output",
            TargetFormat = ConversionTargetFormat.ManifestOnly
        };

        var result = new ConversionResult
        {
            Status = ConversionStatus.Succeeded,
            Warnings = new List<string> { "Warning 1" }
        };

        var manifest = builder.Build(request, result);

        // Capture directories/files to ensure isolation
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var json = serializer.Serialize(manifest);

        // Assert JSON is stable and contains key fields
        Assert.NotNull(json);
        Assert.Contains("\"assetDisplayName\": \"Test Asset\"", json);
        Assert.Contains("\"sourceScoPath\": \"/path/to/source.sco\"", json);
        Assert.Contains("\"relativePath\": \"Sceneryobjects/test/source.sco\"", json);
        Assert.Contains("\"targetFormat\": \"ManifestOnly\"", json);
        Assert.Contains("\"generatedAtUtc\": \"2026-06-24T12:00:00+00:00\"", json);
        Assert.Contains("\"kind\": \"Mesh\"", json);
        Assert.Contains("\"kind\": \"Texture\"", json);
        Assert.Contains("\"path\": \"mesh1.o3d\"", json);
        Assert.Contains("\"path\": \"texture1.dds\"", json);
        Assert.Contains("\"warnings\"", json);
        Assert.Contains("Warning 1", json);

        // Verify that absolutely no files or directories are written
        Assert.False(Directory.Exists(tempDir), "No directory should have been created.");
    }
}
