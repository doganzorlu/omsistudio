using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;
using OmsiStudio.App.ViewModels;
using OmsiStudio.App.Services;

namespace OmsiStudio.OmsiFormat.Tests;

public class MainWindowViewModelTests
{
    private class FakeFolderPickerService : IFolderPickerService
    {
        public string? PresetPath { get; set; }

        public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PresetPath);
        }
    }

    private class FakeAssetScanner : IOmsiAssetScanner
    {
        public List<OmsiAsset> AssetsToReturn { get; } = new();
        public Exception? ExceptionToThrow { get; set; }
        public string? CapturedRootDirectory { get; private set; }

        public async IAsyncEnumerable<OmsiAsset> ScanDirectoryAsync(
            string rootDirectory, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CapturedRootDirectory = rootDirectory;

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            foreach (var asset in AssetsToReturn)
            {
                yield return asset;
                await Task.Yield();
            }
        }
    }

    [Fact]
    public async Task SelectFolderCommand_ShouldTriggerScanner_AndPopulateAssets()
    {
        // Arrange
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset
        {
            DisplayName = "Test Asset 1",
            RelativePath = "Folder/asset1.sco",
            AssetType = OmsiAssetType.SceneryObject
        });

        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker);

        // Act
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("/path/to/omsi", fakeScanner.CapturedRootDirectory);
        Assert.Equal("/path/to/omsi", viewModel.RootDirectory);
        Assert.Single(viewModel.Assets);
        Assert.Equal(1, viewModel.AssetCount);
        Assert.Equal("Test Asset 1", viewModel.Assets[0].DisplayName);
        Assert.False(viewModel.IsEmptyState);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SelectFolderCommand_ShouldSurfaceErrors_WhenScannerFails()
    {
        // Arrange
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var fakeScanner = new FakeAssetScanner
        {
            ExceptionToThrow = new Exception("Disk error")
        };

        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker);

        // Act
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Scan failed: Disk error", viewModel.ErrorMessage);
        Assert.True(viewModel.IsEmptyState);
        Assert.Empty(viewModel.Assets);
        Assert.False(viewModel.IsScanning);
    }

    [Fact]
    public async Task SearchText_ShouldFilterAssets_AndMaintainAssetCount()
    {
        // Arrange
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var fakeScanner = new FakeAssetScanner();
        var asset1 = new OmsiAsset
        {
            DisplayName = "Unique House",
            RelativePath = "Folder/house.sco",
            AssetType = OmsiAssetType.SceneryObject
        };
        var asset2 = new OmsiAsset
        {
            DisplayName = "Standard Tree",
            RelativePath = "Folder/tree.sco",
            AssetType = OmsiAssetType.SceneryObject
        };
        fakeScanner.AssetsToReturn.Add(asset1);
        fakeScanner.AssetsToReturn.Add(asset2);

        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker);
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Verify initial state
        Assert.Equal(2, viewModel.Assets.Count);
        Assert.Equal(2, viewModel.AssetCount);

        // Select an asset
        viewModel.SelectedAsset = asset1;

        // Act: filter by "House"
        viewModel.SearchText = "House";

        // Assert: filtered correctly
        Assert.Single(viewModel.Assets);
        Assert.Equal("Unique House", viewModel.Assets[0].DisplayName);
        // AssetCount should represent total scanned assets, not filtered count
        Assert.Equal(2, viewModel.AssetCount);
        // SelectedAsset should remain assigned because it matches the filter
        Assert.Equal(asset1, viewModel.SelectedAsset);

        // Act: filter by "NonExistent" (will filter out asset1)
        viewModel.SearchText = "NonExistent";

        // Assert: empty assets, and SelectedAsset reset to null because it doesn't match
        Assert.Empty(viewModel.Assets);
        Assert.Null(viewModel.SelectedAsset);

        // Act: clear SearchText
        viewModel.SearchText = "";

        // Assert: restored all assets, SelectedAsset remains null
        Assert.Equal(2, viewModel.Assets.Count);
        Assert.Equal(2, viewModel.AssetCount);
        Assert.Null(viewModel.SelectedAsset);
    }
}
