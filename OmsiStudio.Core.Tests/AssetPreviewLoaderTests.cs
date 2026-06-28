using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.Core.Tests;

/// <summary>
/// Unit tests verifying interface definition and cancellation flow of IAssetPreviewLoader using a fake implementation.
/// </summary>
public class AssetPreviewLoaderTests
{
    private class FakeAssetPreviewLoader : IAssetPreviewLoader
    {
        private readonly AssetPreviewResult _resultToReturn;

        public FakeAssetPreviewLoader(AssetPreviewResult resultToReturn)
        {
            _resultToReturn = resultToReturn;
        }

        public async Task<AssetPreviewResult> LoadAsync(AssetPreviewRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            return _resultToReturn;
        }
    }

    [Fact]
    public async Task FakeLoader_LoadAsync_ReturnsConfiguredResult()
    {
        // Arrange
        var expectedResult = new AssetPreviewResult
        {
            Status = AssetPreviewStatus.Success,
            MeshData = new O3dMeshData()
        };
        var loader = new FakeAssetPreviewLoader(expectedResult);
        var request = new AssetPreviewRequest { AssetId = "asset_1", ModelPath = "model/cube.o3d" };

        // Act
        var result = await loader.LoadAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AssetPreviewStatus.Success, result.Status);
        Assert.NotNull(result.MeshData);
    }

    [Fact]
    public async Task FakeLoader_LoadAsync_WithPreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var expectedResult = new AssetPreviewResult { Status = AssetPreviewStatus.Success };
        var loader = new FakeAssetPreviewLoader(expectedResult);
        var request = new AssetPreviewRequest { AssetId = "asset_1", ModelPath = "model/cube.o3d" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await loader.LoadAsync(request, cts.Token)
        );
    }
}
