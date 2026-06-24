using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Conversion;
using OmsiStudio.Core.Services;
using OmsiStudio.Conversion;

namespace OmsiStudio.Conversion.Tests;

public class AssetConversionTests : IDisposable
{
    private readonly string _dummyAbsoluteDirectory;
    private readonly string _dummyAssetPath;

    public AssetConversionTests()
    {
        var testId = Guid.NewGuid().ToString("N");
        _dummyAbsoluteDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"OmsiConvTest_{testId}"));
        _dummyAssetPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"OmsiConvAsset_{testId}", "Sceneryobjects", "test.sco"));

        Directory.CreateDirectory(_dummyAbsoluteDirectory);
        var assetDir = Path.GetDirectoryName(_dummyAssetPath);
        if (assetDir != null)
        {
            Directory.CreateDirectory(assetDir);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dummyAbsoluteDirectory))
            {
                Directory.Delete(_dummyAbsoluteDirectory, true);
            }
        }
        catch
        {
            // Best effort
        }

        try
        {
            var assetDir = Path.GetDirectoryName(_dummyAssetPath);
            if (assetDir != null && Directory.Exists(assetDir))
            {
                Directory.Delete(assetDir, true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public void ConversionEnums_ShouldDefaultToUnknownZero()
    {
        // Assert Unknown is 0
        Assert.Equal(0, (int)ConversionTargetFormat.Unknown);
        Assert.Equal(0, (int)ConversionStatus.Unknown);

        // Verify other values
        Assert.Equal(ConversionTargetFormat.Unknown, default(ConversionTargetFormat));
        Assert.Equal(ConversionStatus.Unknown, default(ConversionStatus));
    }

    [Fact]
    public async Task ConvertAsync_WithManifestOnlyFormat_ReturnsDeterministicSucceededResult()
    {
        // Arrange
        var service = new AssetConversionService();
        var request = new ConversionRequest
        {
            Asset = new OmsiAsset
            {
                DisplayName = "Test Object",
                SourceScoPath = _dummyAssetPath
            },
            TargetOutputDirectory = _dummyAbsoluteDirectory,
            TargetFormat = ConversionTargetFormat.ManifestOnly
        };

        // Act
        var result = await service.ConvertAsync(request);

        // Assert
        Assert.Equal(ConversionStatus.Succeeded, result.Status);
        Assert.Single(result.OutputFiles);
        Assert.Equal(Path.Combine(_dummyAbsoluteDirectory, "test_manifest.json"), result.OutputFiles[0]);
        Assert.Single(result.Warnings);
        Assert.Contains("placeholder manifest only export", result.Warnings[0]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ConvertAsync_WithUnsupportedFormat_ReturnsFailedResult()
    {
        // Arrange
        var service = new AssetConversionService();
        var request = new ConversionRequest
        {
            Asset = new OmsiAsset
            {
                DisplayName = "Test Object",
                SourceScoPath = _dummyAssetPath
            },
            TargetOutputDirectory = _dummyAbsoluteDirectory,
            TargetFormat = ConversionTargetFormat.Gltf
        };

        // Act
        var result = await service.ConvertAsync(request);

        // Assert
        Assert.Equal(ConversionStatus.Failed, result.Status);
        Assert.Empty(result.OutputFiles);
        Assert.Single(result.Errors);
        Assert.Contains("is not supported yet", result.Errors[0]);
    }

    [Fact]
    public async Task ConvertAsync_WithUnknownFormat_ReturnsFailedResult()
    {
        // Arrange
        var service = new AssetConversionService();
        var request = new ConversionRequest
        {
            Asset = new OmsiAsset
            {
                DisplayName = "Test Object",
                SourceScoPath = _dummyAssetPath
            },
            TargetOutputDirectory = _dummyAbsoluteDirectory,
            TargetFormat = ConversionTargetFormat.Unknown
        };

        // Act
        var result = await service.ConvertAsync(request);

        // Assert
        Assert.Equal(ConversionStatus.Failed, result.Status);
        Assert.Empty(result.OutputFiles);
        Assert.Single(result.Errors);
        Assert.Contains("is not supported yet", result.Errors[0]);
    }

    [Fact]
    public async Task ConvertAsync_WithNullRequest_ReturnsFailedResult()
    {
        // Arrange
        var service = new AssetConversionService();

        // Act
        var result = await service.ConvertAsync(null!);

        // Assert
        Assert.Equal(ConversionStatus.Failed, result.Status);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ConvertAsync_WithMissingAssetPath_ReturnsFailedResult()
    {
        // Arrange
        var service = new AssetConversionService();
        var request = new ConversionRequest
        {
            Asset = new OmsiAsset
            {
                DisplayName = "Test Object",
                SourceScoPath = "" // Invalid
            },
            TargetOutputDirectory = _dummyAbsoluteDirectory,
            TargetFormat = ConversionTargetFormat.ManifestOnly
        };

        // Act
        var result = await service.ConvertAsync(request);

        // Assert
        Assert.Equal(ConversionStatus.Failed, result.Status);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ConvertAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = new AssetConversionService();
        var request = new ConversionRequest
        {
            Asset = new OmsiAsset
            {
                DisplayName = "Test Object",
                SourceScoPath = _dummyAssetPath
            },
            TargetOutputDirectory = _dummyAbsoluteDirectory,
            TargetFormat = ConversionTargetFormat.ManifestOnly
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ConvertAsync(request, cts.Token));
    }

    [Fact]
    public async Task ConvertAsync_WithMissingOrEmptyOutputDirectory_ReturnsFailedResult()
    {
        // Arrange
        var service = new AssetConversionService();
        var request = new ConversionRequest
        {
            Asset = new OmsiAsset
            {
                DisplayName = "Test Object",
                SourceScoPath = _dummyAssetPath
            },
            TargetOutputDirectory = "", // Empty
            TargetFormat = ConversionTargetFormat.ManifestOnly
        };

        // Act
        var result = await service.ConvertAsync(request);

        // Assert
        Assert.Equal(ConversionStatus.Failed, result.Status);
        Assert.Single(result.Errors);
        Assert.Contains("directory is missing or empty", result.Errors[0]);
    }

    [Fact]
    public async Task ConvertAsync_WithRelativeOutputDirectory_ReturnsFailedResult()
    {
        // Arrange
        var service = new AssetConversionService();
        var request = new ConversionRequest
        {
            Asset = new OmsiAsset
            {
                DisplayName = "Test Object",
                SourceScoPath = _dummyAssetPath
            },
            TargetOutputDirectory = "relative/output/dir", // Relative
            TargetFormat = ConversionTargetFormat.ManifestOnly
        };

        // Act
        var result = await service.ConvertAsync(request);

        // Assert
        Assert.Equal(ConversionStatus.Failed, result.Status);
        Assert.Single(result.Errors);
        Assert.Contains("must be an absolute path", result.Errors[0]);
    }

    [Fact]
    public async Task ConvertAsync_WithValidAbsoluteOutputDirectory_Succeeds()
    {
        // Arrange
        var service = new AssetConversionService();
        var request = new ConversionRequest
        {
            Asset = new OmsiAsset
            {
                DisplayName = "Test Object",
                SourceScoPath = _dummyAssetPath
            },
            TargetOutputDirectory = _dummyAbsoluteDirectory, // Absolute
            TargetFormat = ConversionTargetFormat.ManifestOnly
        };

        // Act
        var result = await service.ConvertAsync(request);

        // Assert
        Assert.Equal(ConversionStatus.Succeeded, result.Status);
        Assert.Single(result.OutputFiles);
        Assert.Equal(Path.Combine(_dummyAbsoluteDirectory, "test_manifest.json"), result.OutputFiles[0]);
    }
}
