using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Scanning;
using OmsiStudio.Core.Services;
using OmsiStudio.App.ViewModels;
using OmsiStudio.App.Services;
using OmsiStudio.App.Services.Rendering;
using CommunityToolkit.Mvvm.Input;
using OmsiStudio.OmsiFormat.Parser;
using OmsiStudio.OmsiFormat.Scanner;

namespace OmsiStudio.App.Tests;

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
        public List<string> WarningsToReturn { get; } = new();
        public List<string> ErrorsToReturn { get; } = new();
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

        public Task<OmsiScanResult> ScanAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            return ScanAsync(rootDirectory, null, cancellationToken);
        }

        public Task<OmsiScanResult> ScanAsync(string rootDirectory, IProgress<OmsiScanProgress>? progress, CancellationToken cancellationToken = default)
        {
            CapturedRootDirectory = rootDirectory;

            if (ExceptionToThrow != null)
            {
                return Task.FromException<OmsiScanResult>(ExceptionToThrow);
            }

            if (progress != null)
            {
                for (int i = 0; i < AssetsToReturn.Count; i++)
                {
                    var asset = AssetsToReturn[i];
                    progress.Report(new OmsiScanProgress
                    {
                        DiscoveredFileCount = i + 1,
                        ParsedAssetCount = i + 1,
                        CurrentFilePath = asset.RelativePath,
                        NewAsset = asset
                    });
                }

                if (WarningsToReturn.Count > 0)
                {
                    progress.Report(new OmsiScanProgress
                    {
                        NewWarnings = WarningsToReturn
                    });
                }

                if (ErrorsToReturn.Count > 0)
                {
                    progress.Report(new OmsiScanProgress
                    {
                        NewErrors = ErrorsToReturn
                    });
                }
            }

            return Task.FromResult(new OmsiScanResult
            {
                DiscoveredAssets = AssetsToReturn,
                Warnings = WarningsToReturn,
                Errors = ErrorsToReturn
            });
        }
    }

    private class FakeAppSettingsService : IAppSettingsService
    {
        public string? PresetRoot { get; set; }
        public string? SavedRoot { get; private set; }
        public string? PresetLanguage { get; set; }
        public string? SavedLanguage { get; private set; }
        public Exception? ExceptionToThrowOnSave { get; set; }

        public Task<string?> GetLastOmsiRootAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PresetRoot);
        }

        public Task SaveLastOmsiRootAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrowOnSave != null)
            {
                throw ExceptionToThrowOnSave;
            }
            SavedRoot = rootDirectory;
            return Task.CompletedTask;
        }

        public Task<string?> GetLastLanguageAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PresetLanguage);
        }

        public Task SaveLastLanguageAsync(string cultureName, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrowOnSave != null)
            {
                throw ExceptionToThrowOnSave;
            }
            SavedLanguage = cultureName;
            return Task.CompletedTask;
        }
    }

    private class RecordingUiDispatcher : IUiDispatcher
    {
        public int InvokeCount { get; private set; }
        public bool HasAccess { get; set; }

        public bool CheckAccess() => HasAccess;

        public Task InvokeAsync(Action action)
        {
            InvokeCount++;
            action();
            return Task.CompletedTask;
        }
    }

    private class FakeScanCacheService : IScanCacheService
    {
        public Dictionary<string, OmsiScanCacheEntry> Cache { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int SaveCallsCount { get; private set; }
        public int GetCallsCount { get; private set; }
        public bool ShouldThrow { get; set; }

        public Task<OmsiScanCacheEntry?> GetAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            GetCallsCount++;
            if (ShouldThrow)
            {
                throw new Exception("Cache read error");
            }

            if (Cache.TryGetValue(rootDirectory, out var entry))
            {
                return Task.FromResult<OmsiScanCacheEntry?>(entry);
            }
            return Task.FromResult<OmsiScanCacheEntry?>(null);
        }

        public Task SaveAsync(OmsiScanCacheEntry entry, CancellationToken cancellationToken = default)
        {
            SaveCallsCount++;
            if (ShouldThrow)
            {
                throw new Exception("Cache write error");
            }

            Cache[entry.RootDirectory] = entry;
            return Task.CompletedTask;
        }

        public int DeleteCallsCount { get; private set; }

        public Task DeleteAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            DeleteCallsCount++;
            if (ShouldThrow)
            {
                throw new Exception("Cache delete error");
            }
            Cache.Remove(rootDirectory);
            return Task.CompletedTask;
        }
    }

    private class FakeClipboardService : IClipboardService
    {
        public string? CopiedText { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }
            CopiedText = text;
            return Task.CompletedTask;
        }
    }

    private class FakeFileLauncherService : IFileLauncherService
    {
        public string? OpenedFolderPath { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }
            OpenedFolderPath = folderPath;
            return Task.CompletedTask;
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

        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, new FakeAppSettingsService(), scanCacheService: new NullScanCacheService());

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

        var localizationService = new LocalizationService();
        localizationService.SetCulture("en-US");
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, new FakeAppSettingsService(), new FakeClipboardService(), new FakeFileLauncherService(), localizationService, scanCacheService: new NullScanCacheService());

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

        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, new FakeAppSettingsService(), scanCacheService: new NullScanCacheService());
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

    [Fact]
    public async Task SearchText_ShouldSupport_AdvancedFilteringAndCounting()
    {
        // Arrange
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var fakeScanner = new FakeAssetScanner();

        var asset1 = new OmsiAsset
        {
            DisplayName = "Grand Station",
            RelativePath = "Infrastructure/station.sco",
            Description = "A beautiful big train station",
            Groups = new List<string> { "Traffic", "Rail" },
            ModelReferences = new List<OmsiModelReference>
            {
                new OmsiModelReference("station_base.o3d"),
                new OmsiModelReference("tracks.o3d")
            },
            AssetType = OmsiAssetType.SceneryObject
        };

        var asset2 = new OmsiAsset
        {
            DisplayName = "Station Sign",
            RelativePath = "Infrastructure/sign.sco",
            Description = "Blue sign with name plate",
            Groups = new List<string> { "Traffic", "Signage" },
            ModelReferences = new List<OmsiModelReference>
            {
                new OmsiModelReference("blue_sign.o3d")
            },
            AssetType = OmsiAssetType.SceneryObject
        };

        var asset3 = new OmsiAsset
        {
            DisplayName = "Oak Tree",
            RelativePath = "Nature/tree.sco",
            Description = "A large oak tree",
            Groups = new List<string> { "Flora" },
            ModelReferences = new List<OmsiModelReference>
            {
                new OmsiModelReference("tree.o3d")
            },
            AssetType = OmsiAssetType.SceneryObject
        };

        fakeScanner.AssetsToReturn.Add(asset1);
        fakeScanner.AssetsToReturn.Add(asset2);
        fakeScanner.AssetsToReturn.Add(asset3);

        var localizationService = new LocalizationService();
        localizationService.SetCulture("en-US");
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, new FakeAppSettingsService(), new FakeClipboardService(), new FakeFileLauncherService(), localizationService, scanCacheService: new NullScanCacheService());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // 1. Initial State (No search active)
        Assert.Equal(3, viewModel.AssetCount);
        Assert.Equal(3, viewModel.FilteredAssetCount);
        Assert.False(viewModel.HasActiveSearch);
        Assert.Equal("Objects Found: 3", viewModel.AssetCountDisplay);

        // 2. Case-insensitive search matching display name
        viewModel.SearchText = "station";
        Assert.Equal(2, viewModel.FilteredAssetCount);
        Assert.True(viewModel.HasActiveSearch);
        Assert.Equal("Showing 2 of 3", viewModel.AssetCountDisplay);
        Assert.Contains(asset1, viewModel.Assets);
        Assert.Contains(asset2, viewModel.Assets);
        Assert.DoesNotContain(asset3, viewModel.Assets);

        // 3. Search by mesh path
        viewModel.SearchText = "tracks";
        Assert.Equal(1, viewModel.FilteredAssetCount);
        Assert.Equal("Showing 1 of 3", viewModel.AssetCountDisplay);
        Assert.Contains(asset1, viewModel.Assets);
        Assert.DoesNotContain(asset2, viewModel.Assets);

        // 4. Multi-token AND search (same or different fields)
        // "station" matches displayName, "blue" matches description in asset2
        viewModel.SearchText = "station blue";
        Assert.Equal(1, viewModel.FilteredAssetCount);
        Assert.Equal(asset2, viewModel.Assets[0]);

        // "traffic rail tracks" matches asset1's groups and mesh path
        viewModel.SearchText = "traffic rail tracks";
        Assert.Equal(1, viewModel.FilteredAssetCount);
        Assert.Equal(asset1, viewModel.Assets[0]);

        // 5. Selected asset preservation and clearing
        viewModel.SearchText = ""; // Reset
        viewModel.SelectedAsset = asset2;

        viewModel.SearchText = "sign"; // asset2 remains visible
        Assert.Equal(asset2, viewModel.SelectedAsset);

        viewModel.SearchText = "flora"; // asset2 filtered out
        Assert.Null(viewModel.SelectedAsset);

        // 6. Resetting search restores all assets
        viewModel.SearchText = "   ";
        Assert.Equal(3, viewModel.FilteredAssetCount);
        Assert.False(viewModel.HasActiveSearch);
        Assert.Equal("Objects Found: 3", viewModel.AssetCountDisplay);
    }

    [Fact]

    public async Task StartScanAsync_ShouldPopulateScanWarningsAndErrors_WhenFound()
    {
        // Arrange
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset
        {
            DisplayName = "Test Asset",
            RelativePath = "Folder/asset.sco",
            AssetType = OmsiAssetType.SceneryObject
        });
        fakeScanner.WarningsToReturn.Add("A test warning");
        fakeScanner.ErrorsToReturn.Add("A test error");

        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, new FakeAppSettingsService(), scanCacheService: new NullScanCacheService());

        // Act
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(viewModel.Assets);
        Assert.Single(viewModel.ScanWarnings);
        Assert.Equal("A test warning", viewModel.ScanWarnings[0]);
        Assert.Single(viewModel.ScanErrors);
        Assert.Equal("A test error", viewModel.ScanErrors[0]);
        Assert.Equal(1, viewModel.WarningCount);
        Assert.Equal(1, viewModel.ErrorCount);
        Assert.True(viewModel.HasScanMessages);
    }

    [Fact]
    public async Task AssetGrouping_ShouldGroupAssetsCorrectly()
    {
        // Arrange
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var fakeScanner = new FakeAssetScanner();
        var rootAsset = new OmsiAsset
        {
            DisplayName = "Root Object",
            RelativePath = "root_object.sco",
            AssetType = OmsiAssetType.SceneryObject,
            Groups = new List<string>()
        };
        var subFolderAsset = new OmsiAsset
        {
            DisplayName = "Sub Folder Object",
            RelativePath = "Buildings\\house.sco",
            AssetType = OmsiAssetType.SceneryObject,
            Groups = new List<string> { "Residential", "Houses" }
        };
        var anotherSubFolderAsset = new OmsiAsset
        {
            DisplayName = "Road Object",
            RelativePath = "Roads/street.sco",
            AssetType = OmsiAssetType.SceneryObject,
            Groups = new List<string> { "Infrastructure" }
        };
        fakeScanner.AssetsToReturn.Add(rootAsset);
        fakeScanner.AssetsToReturn.Add(subFolderAsset);
        fakeScanner.AssetsToReturn.Add(anotherSubFolderAsset);

        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, new FakeAppSettingsService(), scanCacheService: new NullScanCacheService());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Debug assertions
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(3, viewModel.Assets.Count);

        // 1. Verify Folder Grouping (Default Mode)
        Assert.Equal(AssetGroupingMode.Folder, viewModel.GroupingMode);
        Assert.Equal(3, viewModel.AssetGroups.Count);

        // (Root) should be at the top
        Assert.Equal("(Root)", viewModel.AssetGroups[0].Name);
        Assert.Single(viewModel.AssetGroups[0].Assets);
        Assert.Equal(rootAsset, viewModel.AssetGroups[0].Assets[0]);

        // Rest should be sorted alphabetically: "Buildings" then "Roads"
        Assert.Equal("Buildings", viewModel.AssetGroups[1].Name);
        Assert.Single(viewModel.AssetGroups[1].Assets);
        Assert.Equal(subFolderAsset, viewModel.AssetGroups[1].Assets[0]);

        Assert.Equal("Roads", viewModel.AssetGroups[2].Name);
        Assert.Single(viewModel.AssetGroups[2].Assets);
        Assert.Equal(anotherSubFolderAsset, viewModel.AssetGroups[2].Assets[0]);

        // 2. Switch to Category Grouping
        viewModel.GroupingMode = AssetGroupingMode.Category;
        Assert.Equal(3, viewModel.AssetGroups.Count);

        // (Ungrouped) at the top
        Assert.Equal("(Ungrouped)", viewModel.AssetGroups[0].Name);
        Assert.Single(viewModel.AssetGroups[0].Assets);
        Assert.Equal(rootAsset, viewModel.AssetGroups[0].Assets[0]);

        // Alphabetical: "Infrastructure" then "Residential"
        Assert.Equal("Infrastructure", viewModel.AssetGroups[1].Name);
        Assert.Single(viewModel.AssetGroups[1].Assets);
        Assert.Equal(anotherSubFolderAsset, viewModel.AssetGroups[1].Assets[0]);

        Assert.Equal("Residential", viewModel.AssetGroups[2].Name);
        Assert.Single(viewModel.AssetGroups[2].Assets);
        Assert.Equal(subFolderAsset, viewModel.AssetGroups[2].Assets[0]);

        // 3. Search filter updates groups
        viewModel.SearchText = "Road";
        Assert.Single(viewModel.AssetGroups);
        Assert.Equal("Infrastructure", viewModel.AssetGroups[0].Name);
        Assert.Single(viewModel.AssetGroups[0].Assets);
        Assert.Equal(anotherSubFolderAsset, viewModel.AssetGroups[0].Assets[0]);

        // Clear Search
        viewModel.SearchText = "";
        Assert.Equal(3, viewModel.AssetGroups.Count);

        // 4. Selecting asset sets SelectedAsset
        var targetAsset = viewModel.AssetGroups[1].Assets[0];
        viewModel.SelectAssetCommand.Execute(targetAsset);
        Assert.Equal(targetAsset, viewModel.SelectedAsset);
    }

    [Fact]
    public async Task LoadSettingsAsync_ShouldSetRootDirectory_WhenSettingsExist()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var fakeSettings = new FakeAppSettingsService { PresetRoot = "/saved/path" };
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, fakeSettings, scanCacheService: new NullScanCacheService());

        // Act
        await viewModel.LoadSettingsAsync();

        // Assert
        Assert.Equal("/saved/path", viewModel.RootDirectory);
    }

    [Fact]
    public async Task LoadSettingsAsync_ShouldLeaveRootDirectoryNull_WhenSettingsAreMissing()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var fakeSettings = new FakeAppSettingsService { PresetRoot = null };
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, fakeSettings, scanCacheService: new NullScanCacheService());

        // Act
        await viewModel.LoadSettingsAsync();

        // Assert
        Assert.Null(viewModel.RootDirectory);
    }

    [Fact]
    public async Task StartScanAsync_ShouldSaveSelectedPath()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/user/selected/path" };
        var fakeSettings = new FakeAppSettingsService();
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, fakeSettings, scanCacheService: new NullScanCacheService());

        // Act
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("/user/selected/path", fakeSettings.SavedRoot);
    }

    [Fact]
    public async Task StartScanAsync_ShouldNotBlockScan_WhenSavingFails()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset
        {
            DisplayName = "Test Asset",
            RelativePath = "asset.sco",
            AssetType = OmsiAssetType.SceneryObject
        });
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path" };
        var fakeSettings = new FakeAppSettingsService
        {
            ExceptionToThrowOnSave = new IOException("Disk failure")
        };
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, fakeSettings, scanCacheService: new NullScanCacheService());

        // Act
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Assert - scan still populates assets successfully despite save failure
        Assert.Single(viewModel.Assets);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task AssetDetailCommands_ShouldBehaveCorrectly()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var fakeSettings = new FakeAppSettingsService();
        var fakeClipboard = new FakeClipboardService();
        var fakeLauncher = new FakeFileLauncherService();

        var asset = new OmsiAsset
        {
            DisplayName = "Detail Asset",
            RelativePath = "Folder/detail.sco",
            SourceScoPath = "/path/to/omsi/Sceneryobjects/Folder/detail.sco",
            AssetType = OmsiAssetType.SceneryObject
        };
        fakeScanner.AssetsToReturn.Add(asset);

        var localizationService = new LocalizationService();
        localizationService.SetCulture("en-US");
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, fakeSettings, fakeClipboard, fakeLauncher, localizationService, scanCacheService: new NullScanCacheService());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // 1. Commands when SelectedAsset is null (should no-op safely)
        Assert.Null(viewModel.SelectedAsset);
        await viewModel.CopyAssetPathCommand.ExecuteAsync(null);
        Assert.Null(fakeClipboard.CopiedText);

        await viewModel.CopyRelativePathCommand.ExecuteAsync(null);
        Assert.Null(fakeClipboard.CopiedText);

        await viewModel.OpenAssetFolderCommand.ExecuteAsync(null);
        Assert.Null(fakeLauncher.OpenedFolderPath);

        // 2. Commands when SelectedAsset is populated
        viewModel.SelectedAsset = asset;

        // Copy Full Path
        await viewModel.CopyAssetPathCommand.ExecuteAsync(null);
        Assert.Equal("/path/to/omsi/Sceneryobjects/Folder/detail.sco", fakeClipboard.CopiedText);

        // Copy Relative Path
        await viewModel.CopyRelativePathCommand.ExecuteAsync(null);
        Assert.Equal("Folder/detail.sco", fakeClipboard.CopiedText);

        // Open Containing Folder
        await viewModel.OpenAssetFolderCommand.ExecuteAsync(null);
        var expectedFolder = Path.GetDirectoryName(asset.SourceScoPath);
        Assert.Equal(expectedFolder, fakeLauncher.OpenedFolderPath);

        // 3. Error Handling (Non-fatal, sets ErrorMessage but does not crash)
        fakeClipboard.ExceptionToThrow = new InvalidOperationException("Clipboard fail");
        fakeLauncher.ExceptionToThrow = new InvalidOperationException("Launch fail");

        // Copy Asset Path should fail gracefully
        viewModel.ErrorMessage = null;
        await viewModel.CopyAssetPathCommand.ExecuteAsync(null);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Contains("Failed to copy asset path", viewModel.ErrorMessage);

        // Copy Relative Path should fail gracefully
        viewModel.ErrorMessage = null;
        await viewModel.CopyRelativePathCommand.ExecuteAsync(null);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Contains("Failed to copy relative path", viewModel.ErrorMessage);

        // Open Folder should fail gracefully
        viewModel.ErrorMessage = null;
        await viewModel.OpenAssetFolderCommand.ExecuteAsync(null);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Contains("Failed to open containing folder", viewModel.ErrorMessage);
    }

    [Fact]
    public void LocalizationService_ShouldManageCulturesAndFallbacks()
    {
        // Arrange
        var service = new LocalizationService();

        // 1. Default culture is Turkish
        Assert.Equal("tr-TR", service.CurrentCulture);
        Assert.Equal("OMSI Kök Klasörünü Seç", service["SelectRootFolder"]);

        // 2. English switch changes keys
        service.SetCulture("en-US");
        Assert.Equal("en-US", service.CurrentCulture);
        Assert.Equal("Select OMSI Root Folder", service["SelectRootFolder"]);

        // 3. Unsupported language falls back safely (to tr-TR)
        service.SetCulture("fr-FR");
        Assert.Equal("tr-TR", service.CurrentCulture);
        Assert.Equal("OMSI Kök Klasörünü Seç", service["SelectRootFolder"]);

        // 4. Missing key returns key itself
        Assert.Equal("NonExistentKey", service["NonExistentKey"]);
    }

    [Fact]
    public async Task MainWindowViewModel_LocalizationIntegration_ShouldUpdateCountsAndTriggerSettingsSave()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var fakeSettings = new FakeAppSettingsService { PresetLanguage = "en-US" };
        var fakeClipboard = new FakeClipboardService();
        var fakeLauncher = new FakeFileLauncherService();
        var localizationService = new LocalizationService();

        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, fakeSettings, fakeClipboard, fakeLauncher, localizationService, scanCacheService: new NullScanCacheService());

        // 1. Initial default culture in VM
        Assert.Equal("tr-TR", localizationService.CurrentCulture);
        Assert.Equal("Bulunan Nesne: 0", viewModel.AssetCountDisplay);
        Assert.Equal("(OMSI klasörü seçilmedi)", viewModel.RootDirectoryDisplay);

        // 2. Load settings should set culture to en-US
        await viewModel.LoadSettingsAsync();
        Assert.Equal("en-US", localizationService.CurrentCulture);
        Assert.Equal("Objects Found: 0", viewModel.AssetCountDisplay);
        Assert.Equal("(No folder selected)", viewModel.RootDirectoryDisplay);

        // Set path and verify it displays the path itself
        viewModel.RootDirectory = "/path/to/omsi";
        Assert.Equal("/path/to/omsi", viewModel.RootDirectoryDisplay);
        viewModel.RootDirectory = null; // Reset

        // 3. SetCultureCommand should change culture, update AssetCountDisplay, and save to settings
        await viewModel.SetCultureCommand.ExecuteAsync("tr-TR");
        Assert.Equal("tr-TR", localizationService.CurrentCulture);
        Assert.Equal("Bulunan Nesne: 0", viewModel.AssetCountDisplay);
        Assert.Equal("tr-TR", fakeSettings.SavedLanguage);

        // Active search formatting test
        viewModel.SearchText = "something";
        viewModel.AssetCount = 5;
        viewModel.FilteredAssetCount = 2;
        Assert.Equal("Gösterilen: 2 / 5", viewModel.AssetCountDisplay);

        // Switch back to English
        await viewModel.SetCultureCommand.ExecuteAsync("en-US");
        Assert.Equal("Showing 2 of 5", viewModel.AssetCountDisplay);
        Assert.Equal("en-US", fakeSettings.SavedLanguage);
    }

    [Fact]
    public void AssetDetail_TexturesExposedAndSafe()
    {
        // 1. Asset with textures
        var assetWithTextures = new OmsiAsset
        {
            DisplayName = "Textured Object",
            SourceScoPath = "/path/to/omsi/Sceneryobjects/textured.sco",
            RelativePath = "textured.sco",
            AssetType = OmsiAssetType.SceneryObject,
            TextureReferences = new List<string> { "tex1.bmp", "tex2.png" }
        };

        Assert.True(assetWithTextures.HasTextures);
        Assert.False(assetWithTextures.HasNoTextures);
        Assert.Equal(2, assetWithTextures.TextureReferences.Count);
        Assert.Contains("tex1.bmp", assetWithTextures.TextureReferences);
        Assert.Contains("tex2.png", assetWithTextures.TextureReferences);

        // 2. Asset without textures
        var assetWithoutTextures = new OmsiAsset
        {
            DisplayName = "Plain Object",
            SourceScoPath = "/path/to/omsi/Sceneryobjects/plain.sco",
            RelativePath = "plain.sco",
            AssetType = OmsiAssetType.SceneryObject
        };

        Assert.False(assetWithoutTextures.HasTextures);
        Assert.True(assetWithoutTextures.HasNoTextures);
        Assert.Empty(assetWithoutTextures.TextureReferences);
    }

    [Fact]
    public async Task ProcessFileLauncherService_ShouldThrowExceptions()
    {
        var service = new OmsiStudio.App.Services.ProcessFileLauncherService();

        // 1. Directory doesn't exist should throw DirectoryNotFoundException
        var nonExistentPath = "/path/to/nonexistent/directory/12345";
        await Assert.ThrowsAsync<System.IO.DirectoryNotFoundException>(() => service.OpenFolderAsync(nonExistentPath));

        // 2. Null/empty path should throw ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() => service.OpenFolderAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.OpenFolderAsync(null!));
    }

    private class TestSyncContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            d(state);
        }
    }

    private class CancellingAssetScanner : IOmsiAssetScanner
    {
        public Action? OnProgressReported { get; set; }

        public IAsyncEnumerable<OmsiAsset> ScanDirectoryAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<OmsiScanResult> ScanAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            return ScanAsync(rootDirectory, null, cancellationToken);
        }

        public async Task<OmsiScanResult> ScanAsync(string rootDirectory, IProgress<OmsiScanProgress>? progress, CancellationToken cancellationToken = default)
        {
            if (progress != null)
            {
                progress.Report(new OmsiScanProgress
                {
                    DiscoveredFileCount = 1,
                    ParsedAssetCount = 1,
                    CurrentFilePath = "Folder/cancelled_asset.sco"
                });
            }

            OnProgressReported?.Invoke();

            await Task.Delay(10);

            if (cancellationToken.IsCancellationRequested)
            {
                return new OmsiScanResult
                {
                    DiscoveredAssets = new List<OmsiAsset>
                    {
                        new OmsiAsset { DisplayName = "Partial Asset", RelativePath = "Folder/partial.sco" }
                    }
                };
            }

            return new OmsiScanResult();
        }
    }

    [Fact]
    public async Task CancelScanCommand_ShouldCancelActiveScan_AndRestoreIdleStateWithoutErrorMessage()
    {
        // Arrange
        var cancellingScanner = new CancellingAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var viewModel = new MainWindowViewModel(cancellingScanner, fakeFolderPicker, new FakeAppSettingsService(), scanCacheService: new NullScanCacheService());

        cancellingScanner.OnProgressReported = () =>
        {
            viewModel.CancelScanCommand.Execute(null);
        };

        var oldContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new TestSyncContext());

            // Act
            await viewModel.StartScanAsync("/path/to/omsi");

            // Assert
            Assert.False(viewModel.IsScanning);
            Assert.Null(viewModel.ErrorMessage);
            Assert.Single(viewModel.Assets);
            Assert.Equal("Partial Asset", viewModel.Assets[0].DisplayName);
            Assert.Equal(viewModel.L["ScanProgressCancelled"], viewModel.ScanProgressText);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(oldContext);
        }
    }

    [Fact]
    public async Task StartScanAsync_ShouldReportProgressToViewModel()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset { DisplayName = "Asset 1", RelativePath = "folder/asset1.sco" });
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, new FakeAppSettingsService(), scanCacheService: new NullScanCacheService());

        var oldContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new TestSyncContext());

            // Act
            var scanTask = viewModel.StartScanAsync("/path/to/omsi");
            await scanTask;

            // Since it succeeded, progress text displays the completed format
            Assert.Equal(string.Format(viewModel.L["ScanProgressCompletedFormat"], viewModel.AssetCount), viewModel.ScanProgressText);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(oldContext);
        }
    }

    [Fact]
    public async Task OmsiAssetScanner_ScanAsync_ShouldRespectCancellationAndReturnPartialResults()
    {
        // Arrange
        var tempDir = Path.Combine(AppContext.BaseDirectory, "CancelTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sceneryObjectsDir = Path.Combine(tempDir, "Sceneryobjects");
        Directory.CreateDirectory(sceneryObjectsDir);

        for (int i = 1; i <= 5; i++)
        {
            File.WriteAllText(Path.Combine(sceneryObjectsDir, $"obj{i}.sco"), $"[friendlyname]\nObject {i}");
        }

        var parser = new ScoFileParser();
        var scanner = new OmsiAssetScanner(parser);

        var cts = new CancellationTokenSource();
        int progressReportCount = 0;

        var oldContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new TestSyncContext());

            var progress = new Progress<OmsiScanProgress>(p =>
            {
                progressReportCount++;
                if (progressReportCount == 2)
                {
                    cts.Cancel(); // Cancel after second file is reported
                }
            });

            // Act
            var result = await scanner.ScanAsync(tempDir, progress, cts.Token);

            // Assert
            Assert.True(result.DiscoveredAssets.Count < 5);
            Assert.True(result.DiscoveredAssets.Count >= 2);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(oldContext);
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch {}
        }
    }

    [Fact]
    public void LocalizationService_ShouldHaveO3dMetadataKeysInBothLanguages()
    {
        // Arrange
        var service = new LocalizationService();
        var keys = new[]
        {
            "O3dMetadataTitle",
            "O3dVersion",
            "O3dEncrypted",
            "O3dMeshCount",
            "O3dVertexCount",
            "O3dTriangleCount",
            "O3dMaterialCount",
            "O3dTextureReferences",
            "O3dNoMetadata",
            "O3dDiagnostics"
        };

        // 1. Verify Turkish (Default)
        service.SetCulture("tr-TR");
        foreach (var key in keys)
        {
            var value = service[key];
            Assert.NotEqual(key, value);
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        // 2. Verify English
        service.SetCulture("en-US");
        foreach (var key in keys)
        {
            var value = service[key];
            Assert.NotEqual(key, value);
            Assert.False(string.IsNullOrWhiteSpace(value));
        }
    }

    [Fact]
    public void MainWindowViewModel_SelectedAssetWithMetadata_PropertiesAreSafeForBinding()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var fakeSettings = new FakeAppSettingsService();
        var fakeClipboard = new FakeClipboardService();
        var fakeLauncher = new FakeFileLauncherService();
        var localizationService = new LocalizationService();
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, fakeSettings, fakeClipboard, fakeLauncher, localizationService, scanCacheService: new NullScanCacheService());

        var metadata = new O3dMetadata
        {
            Version = O3dFormatVersion.Version3,
            IsEncrypted = false,
            MeshCount = 2,
            VertexCount = 100,
            TriangleCount = 50,
            MaterialCount = 1,
            TextureReferences = new List<O3dTextureReference> { new() { Path = "texture.bmp" } }
        };
        var diagnostics = new List<O3dDiagnostic>
        {
            new() { Severity = O3dDiagnosticSeverity.Error, Code = O3dDiagnosticCode.SafetyLimitExceeded, Message = "Safety warning" }
        };

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved)
        {
            Metadata = metadata,
            MetadataStatus = O3dMetadataStatus.Success,
            MetadataDiagnostics = diagnostics
        };

        var asset = new OmsiAsset
        {
            DisplayName = "Test Asset",
            SourceScoPath = "/path/to/asset.sco",
            RelativePath = "TestAsset/asset.sco",
            AssetType = OmsiAssetType.SceneryObject,
            ModelReferences = new List<OmsiModelReference> { modelRef }
        };

        // Act
        viewModel.SelectedAsset = asset;

        // Assert
        Assert.NotNull(viewModel.SelectedAsset);
        Assert.True(viewModel.HasSelectedAsset);
        var refInVm = viewModel.SelectedAsset.ModelReferences[0];
        Assert.True(refInVm.HasMetadata);
        Assert.False(refInVm.HasNoMetadata);
        Assert.True(refInVm.HasMetadataDiagnostics);
        Assert.Equal(O3dFormatVersion.Version3, refInVm.Metadata?.Version);
        Assert.Equal("texture.bmp", refInVm.Metadata?.TextureReferences?[0].Path);
        Assert.Equal("Safety warning", refInVm.MetadataDiagnostics[0].Message);
    }

    [Fact]
    public async Task MainWindowViewModel_ScanResultWithO3dMetadata_PopulatesAndPreservesExpectedData()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var fakeSettings = new FakeAppSettingsService();
        var fakeClipboard = new FakeClipboardService();
        var fakeLauncher = new FakeFileLauncherService();
        var localizationService = new LocalizationService();
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, fakeSettings, fakeClipboard, fakeLauncher, localizationService, scanCacheService: new NullScanCacheService());

        var metadata = new O3dMetadata
        {
            Version = O3dFormatVersion.Version3,
            IsEncrypted = false,
            MeshCount = 2,
            VertexCount = 100,
            TriangleCount = 50,
            MaterialCount = 1
        };

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved)
        {
            Metadata = metadata,
            MetadataStatus = O3dMetadataStatus.Success
        };

        var asset = new OmsiAsset
        {
            DisplayName = "Metadata Asset",
            SourceScoPath = "/path/to/asset.sco",
            RelativePath = "Folder/asset.sco",
            AssetType = OmsiAssetType.SceneryObject,
            ModelReferences = new List<OmsiModelReference> { modelRef }
        };

        fakeScanner.AssetsToReturn.Add(asset);
        fakeScanner.WarningsToReturn.Add("[model.o3d] Model reference metadata warning/error: safety limit hit");

        // Act
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Assert: assets populated
        Assert.Single(viewModel.Assets);
        Assert.Equal("Metadata Asset", viewModel.Assets[0].DisplayName);

        // Act: selection
        viewModel.SelectedAsset = viewModel.Assets[0];

        // Assert: preserves metadata properties on SelectedAsset
        Assert.NotNull(viewModel.SelectedAsset);
        var refInVm = viewModel.SelectedAsset.ModelReferences[0];
        Assert.True(refInVm.HasMetadata);
        Assert.False(refInVm.HasNoMetadata);
        Assert.Equal(O3dFormatVersion.Version3, refInVm.Metadata?.Version);
        Assert.Equal(O3dMetadataStatus.Success, refInVm.MetadataStatus);

        // Assert: scan warnings surfaced in ScanWarnings
        Assert.Single(viewModel.ScanWarnings);
        Assert.Contains("safety limit hit", viewModel.ScanWarnings[0]);
        Assert.Equal(1, viewModel.WarningCount);
        Assert.True(viewModel.HasScanWarnings);
    }

    [Fact]
    public async Task MainWindowViewModel_ScanResultWithMissingMetadata_IsSafeAndHasNoMetadata()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var fakeSettings = new FakeAppSettingsService();
        var fakeClipboard = new FakeClipboardService();
        var fakeLauncher = new FakeFileLauncherService();
        var localizationService = new LocalizationService();
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, fakeSettings, fakeClipboard, fakeLauncher, localizationService, scanCacheService: new NullScanCacheService());

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", false, OmsiModelReferenceResolutionStatus.Missing)
        {
            Metadata = null,
            MetadataStatus = O3dMetadataStatus.Unknown
        };

        var asset = new OmsiAsset
        {
            DisplayName = "Missing Asset",
            SourceScoPath = "/path/to/asset.sco",
            RelativePath = "Folder/asset.sco",
            AssetType = OmsiAssetType.SceneryObject,
            ModelReferences = new List<OmsiModelReference> { modelRef }
        };

        fakeScanner.AssetsToReturn.Add(asset);

        // Act
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        viewModel.SelectedAsset = viewModel.Assets[0];

        // Assert
        Assert.NotNull(viewModel.SelectedAsset);
        var refInVm = viewModel.SelectedAsset.ModelReferences[0];
        Assert.False(refInVm.HasMetadata);
        Assert.True(refInVm.HasNoMetadata);
        Assert.Null(refInVm.Metadata);
        Assert.Equal(O3dMetadataStatus.Unknown, refInVm.MetadataStatus);
    }

    [Fact]
    public async Task MainWindowViewModel_ScanResultWithDiagnosticsButNoMetadata_ExposesDiagnosticsSafely()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var fakeSettings = new FakeAppSettingsService();
        var fakeClipboard = new FakeClipboardService();
        var fakeLauncher = new FakeFileLauncherService();
        var localizationService = new LocalizationService();
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, fakeSettings, fakeClipboard, fakeLauncher, localizationService, scanCacheService: new NullScanCacheService());

        var diagnostics = new List<O3dDiagnostic>
        {
            new() { Severity = O3dDiagnosticSeverity.Error, Code = O3dDiagnosticCode.TruncatedStream, Message = "Truncated file" }
        };

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved)
        {
            Metadata = null,
            MetadataStatus = O3dMetadataStatus.Invalid,
            MetadataDiagnostics = diagnostics
        };

        var asset = new OmsiAsset
        {
            DisplayName = "Diag Asset",
            SourceScoPath = "/path/to/asset.sco",
            RelativePath = "Folder/asset.sco",
            AssetType = OmsiAssetType.SceneryObject,
            ModelReferences = new List<OmsiModelReference> { modelRef }
        };

        fakeScanner.AssetsToReturn.Add(asset);

        // Act
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        viewModel.SelectedAsset = viewModel.Assets[0];

        // Assert
        Assert.NotNull(viewModel.SelectedAsset);
        var refInVm = viewModel.SelectedAsset.ModelReferences[0];
        Assert.False(refInVm.HasMetadata);
        Assert.True(refInVm.HasNoMetadata);
        Assert.True(refInVm.HasMetadataDiagnostics);
        Assert.Single(refInVm.MetadataDiagnostics);
        Assert.Equal("Truncated file", refInVm.MetadataDiagnostics[0].Message);
    }

    private class DelayedAssetScanner : IOmsiAssetScanner
    {
        public TaskCompletionSource FirstAssetReportedTcs { get; } = new();
        public TaskCompletionSource FinishScanTcs { get; } = new();
        public List<OmsiAsset> AssetsToReturn { get; } = new();

        public IAsyncEnumerable<OmsiAsset> ScanDirectoryAsync(string rootDirectory, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<OmsiScanResult> ScanAsync(string rootDirectory, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public async Task<OmsiScanResult> ScanAsync(string rootDirectory, IProgress<OmsiScanProgress>? progress, CancellationToken cancellationToken = default)
        {
            if (progress != null && AssetsToReturn.Count > 0)
            {
                var asset = AssetsToReturn[0];
                progress.Report(new OmsiScanProgress
                {
                    DiscoveredFileCount = 1,
                    ParsedAssetCount = 1,
                    CurrentFilePath = asset.RelativePath,
                    NewAsset = asset
                });
            }

            // Signal that we reported the first asset
            FirstAssetReportedTcs.SetResult();

            // Wait until the test allows us to finish
            await FinishScanTcs.Task;

            if (cancellationToken.IsCancellationRequested)
            {
                return new OmsiScanResult
                {
                    DiscoveredAssets = new List<OmsiAsset> { AssetsToReturn[0] },
                    Warnings = new List<string>(),
                    Errors = new List<string>()
                };
            }

            if (progress != null && AssetsToReturn.Count > 1)
            {
                var asset = AssetsToReturn[1];
                progress.Report(new OmsiScanProgress
                {
                    DiscoveredFileCount = 2,
                    ParsedAssetCount = 2,
                    CurrentFilePath = asset.RelativePath,
                    NewAsset = asset
                });
            }

            return new OmsiScanResult
            {
                DiscoveredAssets = AssetsToReturn,
                Warnings = new List<string>(),
                Errors = new List<string>()
            };
        }
    }

    [Fact]
    public async Task StartScanAsync_ShouldPopulateAssetsIncrementally_BeforeScanCompletes()
    {
        // Arrange
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var delayedScanner = new DelayedAssetScanner();
        var asset1 = new OmsiAsset { DisplayName = "Asset 1", RelativePath = "Folder/a1.sco" };
        var asset2 = new OmsiAsset { DisplayName = "Asset 2", RelativePath = "Folder/a2.sco" };
        delayedScanner.AssetsToReturn.Add(asset1);
        delayedScanner.AssetsToReturn.Add(asset2);

        var viewModel = new MainWindowViewModel(delayedScanner, fakeFolderPicker, new FakeAppSettingsService(), scanCacheService: new NullScanCacheService());

        // Act
        var scanTask = viewModel.StartScanAsync("/path/to/omsi");

        // Wait until the first asset is reported by the background scanner
        await delayedScanner.FirstAssetReportedTcs.Task;

        // Give a small delay/yield for the progress callback to process
        for (int i = 0; i < 50; i++)
        {
            if (viewModel.Assets.Count > 0) break;
            await Task.Delay(10);
        }

        // Assert: the first asset is already in the UI collections even though the scan is not complete
        Assert.Single(viewModel.Assets);
        Assert.Equal("Asset 1", viewModel.Assets[0].DisplayName);
        Assert.True(viewModel.IsScanning);

        // Act: finish the scan
        delayedScanner.FinishScanTcs.SetResult();
        await scanTask;

        // Give a small delay/yield for the second asset progress callback to process
        for (int i = 0; i < 50; i++)
        {
            if (viewModel.Assets.Count > 1) break;
            await Task.Delay(10);
        }

        // Assert: both assets are present after scan completion
        Assert.Equal(2, viewModel.Assets.Count);
        Assert.False(viewModel.IsScanning);
    }

    [Fact]
    public async Task StartScanAsync_ShouldPreservePartialResults_OnCancellation()
    {
        // Arrange
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var delayedScanner = new DelayedAssetScanner();
        var asset1 = new OmsiAsset { DisplayName = "Asset 1", RelativePath = "Folder/a1.sco" };
        var asset2 = new OmsiAsset { DisplayName = "Asset 2", RelativePath = "Folder/a2.sco" };
        delayedScanner.AssetsToReturn.Add(asset1);
        delayedScanner.AssetsToReturn.Add(asset2);

        var viewModel = new MainWindowViewModel(delayedScanner, fakeFolderPicker, new FakeAppSettingsService(), scanCacheService: new NullScanCacheService());

        // Act
        var scanTask = viewModel.StartScanAsync("/path/to/omsi");

        // Wait until first asset is reported
        await delayedScanner.FirstAssetReportedTcs.Task;

        for (int i = 0; i < 50; i++)
        {
            if (viewModel.Assets.Count > 0) break;
            await Task.Delay(10);
        }

        // Cancel the scan using the ViewModel's CancelScanCommand
        viewModel.CancelScanCommand.Execute(null);

        // Finish delayed scan
        delayedScanner.FinishScanTcs.SetResult();
        try
        {
            await scanTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert: asset1 is preserved, but asset2 is not added since scan was cancelled
        Assert.Null(viewModel.ErrorMessage);
        Assert.Single(viewModel.Assets);
        Assert.Equal("Asset 1", viewModel.Assets[0].DisplayName);
        Assert.False(viewModel.IsScanning);
        Assert.Equal(viewModel.L["ScanProgressCancelled"], viewModel.ScanProgressText);
    }

    [Fact]
    public async Task StartScanAsync_ShouldSupportSearchAndFilterUpdates_DuringScan()
    {
        // Arrange
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var delayedScanner = new DelayedAssetScanner();
        var asset1 = new OmsiAsset { DisplayName = "House Object", RelativePath = "Folder/house.sco" };
        var asset2 = new OmsiAsset { DisplayName = "Tree Object", RelativePath = "Folder/tree.sco" };
        delayedScanner.AssetsToReturn.Add(asset1);
        delayedScanner.AssetsToReturn.Add(asset2);

        var viewModel = new MainWindowViewModel(delayedScanner, fakeFolderPicker, new FakeAppSettingsService(), scanCacheService: new NullScanCacheService());

        // Act
        var scanTask = viewModel.StartScanAsync("/path/to/omsi");

        // Wait until first asset (House) is reported
        await delayedScanner.FirstAssetReportedTcs.Task;

        for (int i = 0; i < 50; i++)
        {
            if (viewModel.Assets.Count > 0) break;
            await Task.Delay(10);
        }

        Assert.Single(viewModel.Assets);
        Assert.Equal("House Object", viewModel.Assets[0].DisplayName);

        // Update search text to "Tree" while the scan is still running in background
        viewModel.SearchText = "Tree";

        // This triggers ApplyFilter on the UI/test thread. Since "House Object" doesn't match "Tree", the list should clear.
        Assert.Empty(viewModel.Assets);

        // Resume/finish the scanner, which will report "Tree Object"
        delayedScanner.FinishScanTcs.SetResult();
        await scanTask;

        for (int i = 0; i < 50; i++)
        {
            if (viewModel.Assets.Count > 0) break;
            await Task.Delay(10);
        }

        // Assert: "Tree Object" matches the active filter and is added, while "House Object" remains filtered out
        Assert.Single(viewModel.Assets);
        Assert.Equal("Tree Object", viewModel.Assets[0].DisplayName);
        Assert.Equal(2, viewModel.AssetCount); // Total discovered remains 2
        Assert.Equal(1, viewModel.FilteredAssetCount); // Filtered count is 1
    }

    [Fact]
    public async Task StartScanAsync_ShouldMarshalBackgroundUpdatesThroughInjectedDispatcher()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset
        {
            DisplayName = "Dispatched Asset",
            RelativePath = "Folder/dispatched.sco",
            AssetType = OmsiAssetType.SceneryObject
        });

        var dispatcher = new RecordingUiDispatcher { HasAccess = false };
        var viewModel = new MainWindowViewModel(
            fakeScanner,
            new FakeFolderPickerService(),
            new FakeAppSettingsService(),
            new FakeClipboardService(),
            new FakeFileLauncherService(),
            new LocalizationService(),
            uiDispatcher: dispatcher, scanCacheService: new NullScanCacheService());

        // Act
        await viewModel.StartScanAsync("/path/to/omsi");

        // Assert
        Assert.True(dispatcher.InvokeCount > 0);
        Assert.Single(viewModel.Assets);
        Assert.Equal("Dispatched Asset", viewModel.Assets[0].DisplayName);
        Assert.False(viewModel.IsScanning);
    }

    [Fact]
    public void SelectedAssetTextureReferences_OnlyScoTextures()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, appSettings, scanCacheService: new NullScanCacheService());

        var asset = new OmsiAsset
        {
            DisplayName = "Test Asset",
            RelativePath = "Folder/asset.sco",
            TextureReferences = new List<string> { "Texture1.png", "Texture2.jpg" }
        };

        // Act
        viewModel.SelectedAsset = asset;

        // Assert
        Assert.True(viewModel.HasSelectedAssetTextureReferences);
        Assert.False(viewModel.HasNoSelectedAssetTextureReferences);
        Assert.Equal(2, viewModel.SelectedAssetTextureReferences.Count);

        var tex1 = viewModel.SelectedAssetTextureReferences[0];
        Assert.Equal("Texture1.png", tex1.Path);
        Assert.Equal("SCO dosyası", tex1.Source); // Turkish is default in LocalizationService
        Assert.True(tex1.IsScoSource);

        var tex2 = viewModel.SelectedAssetTextureReferences[1];
        Assert.Equal("Texture2.jpg", tex2.Path);
        Assert.Equal("SCO dosyası", tex2.Source);
        Assert.True(tex2.IsScoSource);
    }

    [Fact]
    public void SelectedAssetTextureReferences_OnlyO3dTextures()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, appSettings, scanCacheService: new NullScanCacheService());

        var asset = new OmsiAsset
        {
            DisplayName = "Test Asset",
            RelativePath = "Folder/asset.sco",
            ModelReferences = new List<OmsiModelReference>
            {
                new OmsiModelReference
                {
                    MeshPath = "models\\Poco_Logo.o3d",
                    Metadata = new O3dMetadata
                    {
                        TextureReferences = new List<O3dTextureReference>
                        {
                            new O3dTextureReference { Path = "O3dTex1.png" },
                            new O3dTextureReference { Path = "O3dTex2.jpg" }
                        }
                    }
                }
            }
        };

        // Act
        viewModel.SelectedAsset = asset;

        // Assert
        Assert.True(viewModel.HasSelectedAssetTextureReferences);
        Assert.False(viewModel.HasNoSelectedAssetTextureReferences);
        Assert.Equal(2, viewModel.SelectedAssetTextureReferences.Count);

        var tex1 = viewModel.SelectedAssetTextureReferences[0];
        Assert.Equal("O3dTex1.png", tex1.Path);
        Assert.Equal("Poco_Logo.o3d", tex1.Source);
        Assert.False(tex1.IsScoSource);

        var tex2 = viewModel.SelectedAssetTextureReferences[1];
        Assert.Equal("O3dTex2.jpg", tex2.Path);
        Assert.Equal("Poco_Logo.o3d", tex2.Source);
        Assert.False(tex2.IsScoSource);
    }

    [Fact]
    public void SelectedAssetTextureReferences_DeduplicatesCaseInsensitively()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, appSettings, scanCacheService: new NullScanCacheService());

        var asset = new OmsiAsset
        {
            DisplayName = "Test Asset",
            RelativePath = "Folder/asset.sco",
            TextureReferences = new List<string> { "Logo.jpg" },
            ModelReferences = new List<OmsiModelReference>
            {
                new OmsiModelReference
                {
                    MeshPath = "Poco_Logo.o3d",
                    Metadata = new O3dMetadata
                    {
                        TextureReferences = new List<O3dTextureReference>
                        {
                            new O3dTextureReference { Path = "logo.JPG" }
                        }
                    }
                }
            }
        };

        // Act
        viewModel.SelectedAsset = asset;

        // Assert
        Assert.Single(viewModel.SelectedAssetTextureReferences);
        var tex = viewModel.SelectedAssetTextureReferences[0];
        Assert.Equal("Logo.jpg", tex.Path);
        Assert.Equal("SCO dosyası", tex.Source);
        Assert.True(tex.IsScoSource);
    }

    [Fact]
    public void SelectedAssetTextureReferences_SelectionChangeClearsAndReloads()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, appSettings, scanCacheService: new NullScanCacheService());

        var asset1 = new OmsiAsset
        {
            DisplayName = "Asset 1",
            RelativePath = "Folder/asset1.sco",
            TextureReferences = new List<string> { "Tex1.png" }
        };

        var asset2 = new OmsiAsset
        {
            DisplayName = "Asset 2",
            RelativePath = "Folder/asset2.sco",
            TextureReferences = new List<string> { "Tex2.png" }
        };

        // Act & Assert 1: Select asset1
        viewModel.SelectedAsset = asset1;
        Assert.Single(viewModel.SelectedAssetTextureReferences);
        Assert.Equal("Tex1.png", viewModel.SelectedAssetTextureReferences[0].Path);
        Assert.True(viewModel.HasSelectedAssetTextureReferences);

        // Act & Assert 2: Select asset2
        viewModel.SelectedAsset = asset2;
        Assert.Single(viewModel.SelectedAssetTextureReferences);
        Assert.Equal("Tex2.png", viewModel.SelectedAssetTextureReferences[0].Path);
        Assert.True(viewModel.HasSelectedAssetTextureReferences);

        // Act & Assert 3: Select null
        viewModel.SelectedAsset = null;
        Assert.Empty(viewModel.SelectedAssetTextureReferences);
        Assert.False(viewModel.HasSelectedAssetTextureReferences);
        Assert.True(viewModel.HasNoSelectedAssetTextureReferences);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithCacheHit_PopulatesAssetsFromCache_WithoutScanning()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService { PresetRoot = "/path/to/omsi" };
        var cacheService = new FakeScanCacheService();
        var cachedEntry = new OmsiScanCacheEntry
        {
            RootDirectory = "/path/to/omsi",
            CachedAtUtc = DateTime.UtcNow,
            Assets = new List<OmsiAsset>
            {
                new OmsiAsset { DisplayName = "Cached Asset 1", RelativePath = "Folder/asset1.sco" }
            },
            Warnings = new List<string> { "Cached Warning" },
            Errors = new List<string> { "Cached Error" }
        };
        cacheService.Cache["/path/to/omsi"] = cachedEntry;

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            scanCacheService: cacheService);

        // Act
        await viewModel.LoadSettingsAsync();

        // Assert
        Assert.Equal("/path/to/omsi", viewModel.RootDirectory);
        Assert.Equal(1, cacheService.GetCallsCount);
        Assert.Null(fakeScanner.CapturedRootDirectory); // Verify NO scan was initiated
        Assert.Single(viewModel.Assets);
        Assert.Equal("Cached Asset 1", viewModel.Assets[0].DisplayName);
        Assert.Single(viewModel.ScanWarnings);
        Assert.Single(viewModel.ScanErrors);
        Assert.Equal(1, viewModel.WarningCount);
        Assert.Equal(1, viewModel.ErrorCount);
        Assert.False(viewModel.IsEmptyState);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithCacheMiss_DoesNotTriggerScan_RemainsEmptyState()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService { PresetRoot = "/path/to/omsi" };
        var cacheService = new FakeScanCacheService(); // Empty cache

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            scanCacheService: cacheService);

        // Act
        await viewModel.LoadSettingsAsync();

        // Assert
        Assert.Equal("/path/to/omsi", viewModel.RootDirectory);
        Assert.Equal(1, cacheService.GetCallsCount);
        Assert.Null(fakeScanner.CapturedRootDirectory); // Verify NO scan was initiated
        Assert.Empty(viewModel.Assets);
        Assert.True(viewModel.IsEmptyState);
    }

    [Fact]
    public async Task SelectFolderCommand_WithCacheHit_PopulatesFromCache_WithoutScanning()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/new-omsi" };
        var appSettings = new FakeAppSettingsService();
        var cacheService = new FakeScanCacheService();
        var cachedEntry = new OmsiScanCacheEntry
        {
            RootDirectory = "/path/to/new-omsi",
            CachedAtUtc = DateTime.UtcNow,
            Assets = new List<OmsiAsset>
            {
                new OmsiAsset { DisplayName = "Cached Asset 2", RelativePath = "Folder/asset2.sco" }
            }
        };
        cacheService.Cache["/path/to/new-omsi"] = cachedEntry;

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            scanCacheService: cacheService);

        // Act
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("/path/to/new-omsi", viewModel.RootDirectory);
        Assert.Equal(1, cacheService.GetCallsCount);
        Assert.Null(fakeScanner.CapturedRootDirectory); // Verify NO scan was initiated
        Assert.Single(viewModel.Assets);
        Assert.Equal("Cached Asset 2", viewModel.Assets[0].DisplayName);
        Assert.False(viewModel.IsEmptyState);
    }

    [Fact]
    public async Task SelectFolderCommand_WithCacheMiss_RunsFullScan_WritesToCache()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset
        {
            DisplayName = "Scanned Asset",
            RelativePath = "Folder/asset.sco"
        });
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/new-omsi" };
        var appSettings = new FakeAppSettingsService();
        var cacheService = new FakeScanCacheService(); // Empty cache

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            scanCacheService: cacheService);

        // Act
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("/path/to/new-omsi", viewModel.RootDirectory);
        Assert.Equal(1, cacheService.GetCallsCount);
        Assert.Equal("/path/to/new-omsi", fakeScanner.CapturedRootDirectory); // Verify full scan executed
        Assert.Single(viewModel.Assets);
        Assert.Equal("Scanned Asset", viewModel.Assets[0].DisplayName);
        Assert.Equal(1, cacheService.SaveCallsCount); // Verify cache is written
        Assert.True(cacheService.Cache.ContainsKey("/path/to/new-omsi"));
        Assert.Single(cacheService.Cache["/path/to/new-omsi"].Assets);
        Assert.Equal("Scanned Asset", cacheService.Cache["/path/to/new-omsi"].Assets[0].DisplayName);
    }

    [Fact]
    public async Task RefreshScanCommand_RunsFullScan_OverwritesCache()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset
        {
            DisplayName = "Fresh Asset",
            RelativePath = "Folder/fresh.sco"
        });
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var cacheService = new FakeScanCacheService();
        var staleEntry = new OmsiScanCacheEntry
        {
            RootDirectory = "/path/to/omsi",
            CachedAtUtc = DateTime.UtcNow.AddHours(-1),
            Assets = new List<OmsiAsset>
            {
                new OmsiAsset { DisplayName = "Stale Asset", RelativePath = "Folder/stale.sco" }
            }
        };
        cacheService.Cache["/path/to/omsi"] = staleEntry;

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            scanCacheService: cacheService);
        viewModel.RootDirectory = "/path/to/omsi";

        // Act
        await viewModel.RefreshScanCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("/path/to/omsi", fakeScanner.CapturedRootDirectory); // Verify full scan executed
        Assert.Single(viewModel.Assets);
        Assert.Equal("Fresh Asset", viewModel.Assets[0].DisplayName);
        Assert.Equal(1, cacheService.SaveCallsCount); // Verify cache updated
        Assert.Equal("Fresh Asset", cacheService.Cache["/path/to/omsi"].Assets[0].DisplayName);
    }

    [Fact]
    public async Task CancelledScan_DoesNotOverwriteCache()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner { ExceptionToThrow = new OperationCanceledException() };
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var cacheService = new FakeScanCacheService();
        var existingEntry = new OmsiScanCacheEntry
        {
            RootDirectory = "/path/to/omsi",
            CachedAtUtc = DateTime.UtcNow,
            Assets = new List<OmsiAsset>
            {
                new OmsiAsset { DisplayName = "Existing Asset", RelativePath = "Folder/existing.sco" }
            }
        };
        cacheService.Cache["/path/to/omsi"] = existingEntry;

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            scanCacheService: cacheService);
        viewModel.RootDirectory = "/path/to/omsi";

        // Act
        await viewModel.RefreshScanCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, cacheService.SaveCallsCount); // Save should NOT be called on cancellation
        Assert.Equal("Existing Asset", cacheService.Cache["/path/to/omsi"].Assets[0].DisplayName); // Existing cache preserved
    }

    [Fact]
    public async Task CacheReadWriteErrors_DoNotCrashApp()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset { DisplayName = "Scanned", RelativePath = "Folder/asset.sco" });
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var appSettings = new FakeAppSettingsService { PresetRoot = "/path/to/omsi" };
        var cacheService = new FakeScanCacheService { ShouldThrow = true }; // Simulate read/write exception

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            scanCacheService: cacheService);

        // Act & Assert 1: LoadSettingsAsync shouldn't crash on cache read error
        var loadException = await Record.ExceptionAsync(() => viewModel.LoadSettingsAsync());
        Assert.Null(loadException);

        // Act & Assert 2: SelectFolderCommand shouldn't crash on cache read/write errors
        var selectException = await Record.ExceptionAsync(() => viewModel.SelectFolderCommand.ExecuteAsync(null));
        Assert.Null(selectException);
    }

    [Fact]
    public async Task StartScanAsync_WithDelayedDispatcher_EnsuresDeterministicSequence()
    {
        // Arrange
        var events = new List<string>();

        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset
        {
            DisplayName = "Test Asset",
            RelativePath = "Folder/asset.sco",
            AssetType = OmsiAssetType.SceneryObject
        });

        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var appSettings = new FakeAppSettingsService();
        var cacheService = new LoggedScanCacheService(events);
        var delayedDispatcher = new LoggedDelayedUiDispatcher(events);

        var viewModel = new MainWindowViewModel(
            fakeScanner, 
            fakeFolderPicker, 
            appSettings,
            new FakeClipboardService(), 
            new FakeFileLauncherService(), 
            new LocalizationService(),
            uiDispatcher: delayedDispatcher,
            scanCacheService: cacheService);

        // Act
        await viewModel.StartScanAsync("/path/to/omsi");

        // Assert
        int progressStart = events.IndexOf("InvokeAsync_Start");
        int progressEnd = events.IndexOf("InvokeAsync_End");
        
        int finalMergeStart = events.IndexOf("InvokeAsync_Start", progressEnd + 1);
        int finalMergeEnd = events.IndexOf("InvokeAsync_End", progressEnd + 1);
        
        int cacheSaveStart = events.IndexOf("Cache_SaveStart");
        int cacheSaveEnd = events.IndexOf("Cache_SaveEnd");
        
        int isScanningFalseStart = events.IndexOf("InvokeAsync_Start", finalMergeEnd + 1);
        int isScanningFalseEnd = events.IndexOf("InvokeAsync_End", finalMergeEnd + 1);

        // Verify all events were recorded
        Assert.NotEqual(-1, progressStart);
        Assert.NotEqual(-1, progressEnd);
        Assert.NotEqual(-1, finalMergeStart);
        Assert.NotEqual(-1, finalMergeEnd);
        Assert.NotEqual(-1, cacheSaveStart);
        Assert.NotEqual(-1, cacheSaveEnd);
        Assert.NotEqual(-1, isScanningFalseStart);
        Assert.NotEqual(-1, isScanningFalseEnd);

        // Verify correct chronological sequence
        Assert.True(progressStart < progressEnd, "Progress update must start before it ends");
        Assert.True(progressEnd < finalMergeStart, "Progress update must fully finish before final merge starts");
        Assert.True(finalMergeStart < finalMergeEnd, "Final merge must start before it ends");
        Assert.True(finalMergeEnd < cacheSaveStart, "Final merge must finish before cache save starts");
        Assert.True(cacheSaveStart < cacheSaveEnd, "Cache save must start before it ends");
        Assert.True(cacheSaveEnd < isScanningFalseStart, "Cache save must finish before IsScanning is set to false");
        Assert.True(isScanningFalseStart < isScanningFalseEnd, "IsScanning = false setting must start before it ends");
    }

    private class LoggedDelayedUiDispatcher : IUiDispatcher
    {
        private readonly List<string> _events;

        public LoggedDelayedUiDispatcher(List<string> events)
        {
            _events = events;
        }

        public bool CheckAccess() => false;

        public async Task InvokeAsync(Action action)
        {
            _events.Add("InvokeAsync_Start");
            await Task.Delay(50); // delay to simulate dispatcher lag
            action();
            _events.Add("InvokeAsync_End");
        }
    }

    private class LoggedScanCacheService : IScanCacheService
    {
        private readonly List<string> _events;

        public LoggedScanCacheService(List<string> events)
        {
            _events = events;
        }

        public Task<OmsiScanCacheEntry?> GetAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<OmsiScanCacheEntry?>(null);
        }

        public Task SaveAsync(OmsiScanCacheEntry entry, CancellationToken cancellationToken = default)
        {
            _events.Add("Cache_SaveStart");
            _events.Add("Cache_SaveEnd");
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LoadFromCache_SetsStatusTextCorrectly()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var appSettings = new FakeAppSettingsService();
        var cacheService = new FakeScanCacheService();
        
        var cachedTime = new DateTime(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);
        var entry = new OmsiScanCacheEntry
        {
            RootDirectory = "/path/to/omsi",
            CachedAtUtc = cachedTime,
            Assets = new List<OmsiAsset>
            {
                new OmsiAsset { DisplayName = "Asset 1", RelativePath = "Folder/asset1.sco" },
                new OmsiAsset { DisplayName = "Asset 2", RelativePath = "Folder/asset2.sco" }
            }
        };
        cacheService.Cache["/path/to/omsi"] = entry;

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            scanCacheService: cacheService);

        // Act
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("Önbellekten yüklendi", viewModel.ScanProgressText);
        Assert.Contains("2 nesne", viewModel.ScanProgressText);
        
        var localTimeStr = cachedTime.ToLocalTime().ToString("g");
        Assert.Contains(localTimeStr, viewModel.ScanProgressText);
    }

    [Fact]
    public async Task Scanning_DisablesCommandsAndUpdatesLiveStatusText()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset { DisplayName = "Scanned Asset", RelativePath = "Folder/scanned.sco" });
        
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var appSettings = new FakeAppSettingsService();
        var cacheService = new FakeScanCacheService();

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            scanCacheService: cacheService);

        // Verify initial state
        Assert.True(viewModel.SelectFolderCommand.CanExecute(null));
        Assert.False(viewModel.RefreshScanCommand.CanExecute(null));
        Assert.False(viewModel.ClearCacheAndRefreshCommand.CanExecute(null));

        // Act - Start scanning
        var scanTask = viewModel.StartScanAsync("/path/to/omsi");

        // During scan, IsScanning is true, so commands should be disabled
        Assert.True(viewModel.IsScanning);
        Assert.False(viewModel.SelectFolderCommand.CanExecute(null));
        Assert.False(viewModel.RefreshScanCommand.CanExecute(null));
        Assert.False(viewModel.ClearCacheAndRefreshCommand.CanExecute(null));

        await scanTask;

        // After scan completes
        Assert.False(viewModel.IsScanning);
        Assert.True(viewModel.SelectFolderCommand.CanExecute(null));
        Assert.True(viewModel.RefreshScanCommand.CanExecute(null));
        Assert.True(viewModel.ClearCacheAndRefreshCommand.CanExecute(null));

        // Scan completed status text is updated with live results
        Assert.Contains("Tarama tamamlandı", viewModel.ScanProgressText);
        Assert.Contains("1 nesne", viewModel.ScanProgressText);
    }

    [Fact]
    public async Task ClearCacheAndRefreshCommand_DeletesCache_ThenScans_AndSavesNewCache()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset { DisplayName = "Scanned Asset", RelativePath = "Folder/scanned.sco" });
        
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var appSettings = new FakeAppSettingsService();
        var cacheService = new FakeScanCacheService();
        cacheService.Cache["/path/to/omsi"] = new OmsiScanCacheEntry
        {
            RootDirectory = "/path/to/omsi",
            Assets = new List<OmsiAsset>()
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            scanCacheService: cacheService);
        viewModel.RootDirectory = "/path/to/omsi";

        // Verify initial state
        Assert.True(viewModel.ClearCacheAndRefreshCommand.CanExecute(null));
        Assert.Single(cacheService.Cache);

        // Act
        await viewModel.ClearCacheAndRefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, cacheService.DeleteCallsCount);
        Assert.Equal(1, cacheService.SaveCallsCount);
        Assert.Single(cacheService.Cache);
        Assert.Single(cacheService.Cache["/path/to/omsi"].Assets);
        Assert.Equal("Scanned Asset", cacheService.Cache["/path/to/omsi"].Assets[0].DisplayName);
    }

    [Fact]
    public async Task ClearCacheAndRefreshCommand_DeleteFailure_DoesNotCrashApp()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var appSettings = new FakeAppSettingsService();
        
        var cacheService = new FakeScanCacheService();
        cacheService.ShouldThrow = true;

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            scanCacheService: cacheService);
        viewModel.RootDirectory = "/path/to/omsi";

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => viewModel.ClearCacheAndRefreshCommand.ExecuteAsync(null));
        Assert.Null(exception);
        Assert.Equal(1, cacheService.DeleteCallsCount);
    }

    [Fact]
    public async Task CultureChange_UpdatesCacheStatusText_Dynamically()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var appSettings = new FakeAppSettingsService();
        var cacheService = new FakeScanCacheService();
        var localization = new LocalizationService();

        var cachedTime = new DateTime(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);
        var entry = new OmsiScanCacheEntry
        {
            RootDirectory = "/path/to/omsi",
            CachedAtUtc = cachedTime,
            Assets = new List<OmsiAsset>
            {
                new OmsiAsset { DisplayName = "Asset 1", RelativePath = "Folder/asset1.sco" }
            }
        };
        cacheService.Cache["/path/to/omsi"] = entry;

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            scanCacheService: cacheService);

        // Load cache first in TR
        localization.SetCulture("tr-TR");
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        // Assert TR progress text
        Assert.Contains("Önbellekten yüklendi", viewModel.ScanProgressText);
        Assert.Contains("1 nesne", viewModel.ScanProgressText);

        // Act - Change culture to EN
        viewModel.SetCultureCommand.Execute("en-US");

        // Assert EN progress text
        Assert.Contains("Loaded from cache", viewModel.ScanProgressText);
        Assert.Contains("1 objects", viewModel.ScanProgressText);
    }

    [Fact]
    public async Task CultureChange_UpdatesCompletedScanStatusText_Dynamically()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        fakeScanner.AssetsToReturn.Add(new OmsiAsset { DisplayName = "Asset 1", RelativePath = "Folder/asset1.sco" });
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var appSettings = new FakeAppSettingsService();
        var cacheService = new FakeScanCacheService();
        var localization = new LocalizationService();

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            scanCacheService: cacheService);

        // Scan first in TR
        localization.SetCulture("tr-TR");
        await viewModel.StartScanAsync("/path/to/omsi");

        // Assert TR progress text
        Assert.Contains("Tarama tamamlandı", viewModel.ScanProgressText);
        Assert.Contains("1 nesne", viewModel.ScanProgressText);

        // Act - Change culture to EN
        viewModel.SetCultureCommand.Execute("en-US");

        // Assert EN progress text
        Assert.Contains("Scanning completed", viewModel.ScanProgressText);
        Assert.Contains("1 objects", viewModel.ScanProgressText);
    }

    [Fact]
    public async Task CultureChange_UpdatesScanningStatusText_Dynamically()
    {
        // Arrange
        var fakeFolderPicker = new FakeFolderPickerService { PresetPath = "/path/to/omsi" };
        var appSettings = new FakeAppSettingsService();
        var cacheService = new FakeScanCacheService();
        var localization = new LocalizationService();

        var tcsScannerReported = new TaskCompletionSource<bool>();
        var tcsResumeScanner = new TaskCompletionSource<bool>();

        var testScanner = new BlockableAssetScanner(tcsScannerReported, tcsResumeScanner);

        var viewModel = new MainWindowViewModel(
            testScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            scanCacheService: cacheService);

        localization.SetCulture("tr-TR");

        // Act - Start scanning
        var scanTask = viewModel.StartScanAsync("/path/to/omsi");

        // Wait until the scanner reports progress and is blocked
        await tcsScannerReported.Task;

        // Verify the status text in TR
        Assert.Contains("Tarama", viewModel.ScanProgressText);
        Assert.Contains("1", viewModel.ScanProgressText);

        // Change culture to EN
        viewModel.SetCultureCommand.Execute("en-US");

        // Verify the status text in EN
        Assert.Contains("Scanning", viewModel.ScanProgressText);
        Assert.Contains("1", viewModel.ScanProgressText);

        // Resume scanner and wait for completion
        tcsResumeScanner.SetResult(true);
        await scanTask;
    }

    private class BlockableAssetScanner : IOmsiAssetScanner
    {
        private readonly TaskCompletionSource<bool> _reportedTcs;
        private readonly TaskCompletionSource<bool> _resumeTcs;

        public BlockableAssetScanner(TaskCompletionSource<bool> reportedTcs, TaskCompletionSource<bool> resumeTcs)
        {
            _reportedTcs = reportedTcs;
            _resumeTcs = resumeTcs;
        }

        public IAsyncEnumerable<OmsiAsset> ScanDirectoryAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<OmsiScanResult> ScanAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            return ScanAsync(rootDirectory, null, cancellationToken);
        }

        public async Task<OmsiScanResult> ScanAsync(string rootDirectory, IProgress<OmsiScanProgress>? progress, CancellationToken cancellationToken = default)
        {
            var asset = new OmsiAsset { DisplayName = "Asset 1", RelativePath = "Folder/asset1.sco" };
            
            if (progress != null)
            {
                progress.Report(new OmsiScanProgress
                {
                    DiscoveredFileCount = 1,
                    ParsedAssetCount = 1,
                    CurrentFilePath = asset.RelativePath,
                    NewAsset = asset
                });
            }

            _reportedTcs.SetResult(true);
            await _resumeTcs.Task;

            return new OmsiScanResult
            {
                DiscoveredAssets = new List<OmsiAsset> { asset },
                Warnings = new List<string>(),
                Errors = new List<string>()
            };
        }
    }

    [Fact]
    public void Preview_NullLoader_FallbackWorks()
    {
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        
        var viewModel = new MainWindowViewModel(fakeScanner, fakeFolderPicker, appSettings, new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService());
        
        Assert.NotNull(viewModel);
        Assert.Equal(AssetPreviewStatus.Idle, viewModel.PreviewStatus);
        Assert.Null(viewModel.PreviewResult);
        Assert.False(viewModel.HasSelectedPreviewModelReference);
    }

    [Fact]
    public async Task Preview_LoadSuccessState_ShouldUpdateViewModelState()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        
        var expectedResult = new AssetPreviewResult
        {
            Status = AssetPreviewStatus.Success,
            MeshData = new O3dMeshData(),
            Diagnostics = []
        };
        
        fakeLoader.LoadFunc = (req, token) => Task.FromResult(expectedResult);

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader);

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);
        var initialRenderVersion = viewModel.RenderVersion;

        // Act
        viewModel.SelectedPreviewModelReference = modelRef;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null)
        {
            await execution;
        }

        // Assert
        Assert.Equal(AssetPreviewStatus.Success, viewModel.PreviewStatus);
        Assert.Equal(expectedResult, viewModel.PreviewResult);
        Assert.True(viewModel.HasPreview);
        Assert.False(viewModel.HasPreviewDiagnostics);
        Assert.Empty(viewModel.PreviewDiagnostics);
        Assert.True(viewModel.RenderVersion > initialRenderVersion);
    }

    [Theory]
    [InlineData(AssetPreviewStatus.Missing)]
    [InlineData(AssetPreviewStatus.Invalid)]
    [InlineData(AssetPreviewStatus.Failed)]
    public async Task Preview_DiagnosticStates_ShouldReflectToViewModelState(AssetPreviewStatus status)
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        
        var expectedResult = new AssetPreviewResult
        {
            Status = status,
            Diagnostics = new List<O3dDiagnostic>
            {
                new() { Severity = O3dDiagnosticSeverity.Error, Message = "Test Diagnostic" }
            }
        };
        
        fakeLoader.LoadFunc = (req, token) => Task.FromResult(expectedResult);

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader);

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // Act
        viewModel.SelectedPreviewModelReference = modelRef;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null)
        {
            await execution;
        }

        // Assert
        Assert.Equal(status, viewModel.PreviewStatus);
        Assert.Equal(expectedResult, viewModel.PreviewResult);
        Assert.False(viewModel.HasPreview);
        Assert.True(viewModel.HasPreviewDiagnostics);
        Assert.Single(viewModel.PreviewDiagnostics);
        Assert.Equal("Test Diagnostic", viewModel.PreviewDiagnostics[0].Message);
    }

    [Fact]
    public async Task Preview_Cancellation_ShouldTransitionToCancelledState()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        
        var tcsLoaderStart = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcsLoaderResume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        fakeLoader.LoadFunc = async (req, token) =>
        {
            tcsLoaderStart.SetResult(true);
            try
            {
                await Task.Delay(5000, token);
                return new AssetPreviewResult { Status = AssetPreviewStatus.Success };
            }
            catch (OperationCanceledException)
            {
                return new AssetPreviewResult { Status = AssetPreviewStatus.Cancelled };
            }
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader);

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // Act - Trigger load preview
        viewModel.SelectedPreviewModelReference = modelRef;

        // Wait until load starts
        await tcsLoaderStart.Task;

        // Verify status is Loading
        Assert.Equal(AssetPreviewStatus.Loading, viewModel.PreviewStatus);
        Assert.True(viewModel.IsPreviewLoading);

        // Cancel
        viewModel.CancelPreviewCommand.Execute(null);

        // Wait for load task to complete
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null)
        {
            await execution;
        }

        // Assert
        Assert.Equal(AssetPreviewStatus.Cancelled, viewModel.PreviewStatus);
        Assert.False(viewModel.IsPreviewLoading);
    }

    [Fact]
    public void Preview_CultureChange_UpdatesStatusTextDynamically()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var localization = new LocalizationService();

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization);

        // Set state to Loading
        localization.SetCulture("tr-TR");
        viewModel.LoadPreviewCommand.Execute(new OmsiModelReference("mesh.o3d"));
        
        // Wait/Ensure it's loading (since fake loader is Idle by default, but we can set it explicitly or check state)
        viewModel.PreviewStatus = AssetPreviewStatus.Loading;
        Assert.Contains("Yükleniyor...", viewModel.PreviewStatusText);

        // Act - Change culture to EN
        viewModel.SetCultureCommand.Execute("en-US");

        // Assert EN status text
        Assert.Contains("Loading...", viewModel.PreviewStatusText);
    }

    [Fact]
    public async Task Preview_WarningOnlyDiagnostics_ShouldSetHasPreviewDiagnosticsToTrue()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        
        var expectedResult = new AssetPreviewResult
        {
            Status = AssetPreviewStatus.Success,
            Diagnostics = new List<O3dDiagnostic>
            {
                new() { Severity = O3dDiagnosticSeverity.Warning, Message = "Test Warning Diagnostic" }
            }
        };
        
        fakeLoader.LoadFunc = (req, token) => Task.FromResult(expectedResult);

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader);

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // Act
        viewModel.SelectedPreviewModelReference = modelRef;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null)
        {
            await execution;
        }

        // Assert
        Assert.True(viewModel.HasPreviewDiagnostics);
        Assert.Single(viewModel.PreviewDiagnostics);
        Assert.Equal("Test Warning Diagnostic", viewModel.PreviewDiagnostics[0].Message);
        Assert.Equal(O3dDiagnosticSeverity.Warning, viewModel.PreviewDiagnostics[0].Severity);
    }

    [Fact]
    public async Task Preview_DiagnosticsStyling_WarningVsError_ShouldHaveCorrectColors()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        
        var warningResult = new AssetPreviewResult
        {
            Status = AssetPreviewStatus.Success,
            Diagnostics = new List<O3dDiagnostic>
            {
                new() { Severity = O3dDiagnosticSeverity.Warning, Message = "A Warning" }
            }
        };
        
        fakeLoader.LoadFunc = (req, token) => Task.FromResult(warningResult);

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader);

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // Act - Load warning only
        viewModel.SelectedPreviewModelReference = modelRef;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        // Assert - Warning Colors (Amber/Orange)
        Assert.Equal("#f59e0b", viewModel.PreviewDiagnosticsBorderBrush);
        Assert.Equal("#2b2214", viewModel.PreviewDiagnosticsBackground);
        Assert.Equal("#fbbf24", viewModel.PreviewDiagnosticsHeaderColor);
        Assert.Equal("#fde68a", viewModel.PreviewDiagnosticsTextColor);

        // Act - Load error
        var errorResult = new AssetPreviewResult
        {
            Status = AssetPreviewStatus.Failed,
            Diagnostics = new List<O3dDiagnostic>
            {
                new() { Severity = O3dDiagnosticSeverity.Error, Message = "An Error" }
            }
        };
        fakeLoader.LoadFunc = (req, token) => Task.FromResult(errorResult);
        await viewModel.LoadPreviewCommand.ExecuteAsync(null);

        // Assert - Diagnostics State
        Assert.Equal(AssetPreviewStatus.Failed, viewModel.PreviewStatus);
        Assert.NotNull(viewModel.PreviewResult);
        Assert.Single(viewModel.PreviewDiagnostics);
        Assert.Equal(O3dDiagnosticSeverity.Error, viewModel.PreviewDiagnostics[0].Severity);

        // Assert - Error Colors (Red)
        Assert.Equal("#ef4444", viewModel.PreviewDiagnosticsBorderBrush);
        Assert.Equal("#2d1c1c", viewModel.PreviewDiagnosticsBackground);
        Assert.Equal("#f87171", viewModel.PreviewDiagnosticsHeaderColor);
        Assert.Equal("#fca5a5", viewModel.PreviewDiagnosticsTextColor);
    }

    [Fact]
    public void Preview_DebugTriangleToggle_PropagatesToRendererHost_AndUpdatesHasPreview()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeRendererHost = new NullRendererHost();
        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            rendererHost: fakeRendererHost);

        Assert.False(viewModel.DebugTriangleEnabled);
        Assert.False(viewModel.HasPreview);
        Assert.False(fakeRendererHost.DebugTriangleEnabled);

        // Act
        viewModel.DebugTriangleEnabled = true;

        // Assert
        Assert.True(viewModel.DebugTriangleEnabled);
        Assert.True(viewModel.HasPreview);
        Assert.True(fakeRendererHost.DebugTriangleEnabled);

        // Act - reset
        viewModel.DebugTriangleEnabled = false;

        // Assert
        Assert.False(viewModel.DebugTriangleEnabled);
        Assert.False(viewModel.HasPreview);
        Assert.False(fakeRendererHost.DebugTriangleEnabled);
    }

    [Fact]
    public void Preview_NotifyRendererDebugStateChanged_TriggersPropertyChangeNotifications()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService());

        var triggeredProperties = new List<string>();
        viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName != null)
            {
                triggeredProperties.Add(e.PropertyName);
            }
        };

        // Act
        viewModel.NotifyRendererDebugStateChanged();

        // Assert
        Assert.Contains(nameof(viewModel.LastFrameDrawAttempted), triggeredProperties);
        Assert.Contains(nameof(viewModel.LastFrameUploadedVertexCount), triggeredProperties);
        Assert.Contains(nameof(viewModel.LastFrameUploadedIndexCount), triggeredProperties);
        Assert.Contains(nameof(viewModel.LastGlError), triggeredProperties);
    }

    [Fact]
    public void Preview_ObservableDiagnosticsProperties_RaisePropertyChanged()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService());

        var triggeredProperties = new List<string>();
        viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName != null)
            {
                triggeredProperties.Add(e.PropertyName);
            }
        };

        // Act
        viewModel.IsOpenGlInitialized = true;
        viewModel.IsRendererHostInitialized = true;
        viewModel.OpenGlRenderCallCount = 5;
        viewModel.LastViewportError = "Some GL Error";
        viewModel.ShowBoundingBox = true;
        viewModel.EnableExperimentalGlPreview = true;

        // Assert
        Assert.Contains(nameof(viewModel.IsOpenGlInitialized), triggeredProperties);
        Assert.Contains(nameof(viewModel.IsRendererHostInitialized), triggeredProperties);
        Assert.Contains(nameof(viewModel.OpenGlRenderCallCount), triggeredProperties);
        Assert.Contains(nameof(viewModel.LastViewportError), triggeredProperties);
        Assert.Contains(nameof(viewModel.ShowBoundingBox), triggeredProperties);
        Assert.Contains(nameof(viewModel.EnableExperimentalGlPreview), triggeredProperties);
    }

    [Fact]
    public void Preview_VisualModeProperties_WorkCorrectly()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService());

        var triggeredProperties = new List<string>();
        viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName != null)
            {
                triggeredProperties.Add(e.PropertyName);
            }
        };

        // Assert Default
        Assert.Equal(SoftwareViewportVisualMode.TexturedSolid, viewModel.VisualMode);
        Assert.Equal(0, viewModel.VisualModeInt);

        // Act
        viewModel.VisualMode = SoftwareViewportVisualMode.TexturedWireframe;

        // Assert
        Assert.Equal(SoftwareViewportVisualMode.TexturedWireframe, viewModel.VisualMode);
        Assert.Equal(1, viewModel.VisualModeInt);
        Assert.Contains(nameof(viewModel.VisualMode), triggeredProperties);
        Assert.Contains(nameof(viewModel.VisualModeInt), triggeredProperties);

        // Act 2
        triggeredProperties.Clear();
        viewModel.VisualModeInt = 4; // Wireframe

        // Assert 2
        Assert.Equal(SoftwareViewportVisualMode.Wireframe, viewModel.VisualMode);
        Assert.Equal(4, viewModel.VisualModeInt);
        Assert.Contains(nameof(viewModel.VisualMode), triggeredProperties);
        Assert.Contains(nameof(viewModel.VisualModeInt), triggeredProperties);
    }

    [Fact]
    public void Preview_CultureChange_UpdatesPreviewMaterialsLocalization()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var localization = new LocalizationService();
        localization.SetCulture("en-US");

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization);

        var meshData = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() },
            MaterialSlots = new List<O3dMaterialSlot> 
            { 
                new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = null }
            }
        };
        viewModel.PreviewResult = new AssetPreviewResult
        {
            Status = AssetPreviewStatus.Success,
            MeshData = meshData
        };

        // Assert initial English "(No texture)"
        Assert.Single(viewModel.PreviewMaterials);
        Assert.Equal("(No texture)", viewModel.PreviewMaterials[0].TextureReference);

        // Act - Switch to Turkish
        localization.SetCulture("tr-TR");

        // Assert Turkish "(Doku yok)"
        Assert.Equal("(Doku yok)", viewModel.PreviewMaterials[0].TextureReference);
    }

    [Fact]
    public void Preview_CultureChange_UpdatesNewLocalizationKeys()
    {
        // Arrange
        var localization = new LocalizationService();

        // Act - Culture is TR
        localization.SetCulture("tr-TR");
        var trPanelTitle = localization["PreviewPanelTitle"];
        var trCancel = localization["CancelPreview"];
        var trButton = localization["PreviewButton"];
        var trPlaceholder = localization["PreviewPanelPlaceholder"];
        var trMeshSummary = localization["PreviewMeshSummary"];
        var trBounds = localization["PreviewBounds"];
        var trNoModelSelected = localization["PreviewNoModelSelected"];
        var trViewportInitFailed = localization["PreviewViewportInitFailed"];

        // Assert TR translations
        Assert.Equal("Model Önizleme Durumu", trPanelTitle);
        Assert.Equal("İptal Et", trCancel);
        Assert.Equal("Önizle", trButton);
        Assert.Equal("3D modeli görüntülemek için bir mesh dosyası seçin ve Önizle butonuna tıklayın.", trPlaceholder);
        Assert.Equal("Köşeler (Vertices): {0} | Üçgenler (Triangles): {1} | Materyaller: {2}", trMeshSummary);
        Assert.Equal("Sınırlar: {0}", trBounds);
        Assert.Equal("Önizleme yapılacak model seçilmedi.", trNoModelSelected);
        Assert.Equal("OpenGL Önizleme Alanı Başlatılamadı", trViewportInitFailed);

        // Act - Culture is EN
        localization.SetCulture("en-US");
        var enPanelTitle = localization["PreviewPanelTitle"];
        var enCancel = localization["CancelPreview"];
        var enButton = localization["PreviewButton"];
        var enPlaceholder = localization["PreviewPanelPlaceholder"];
        var enMeshSummary = localization["PreviewMeshSummary"];
        var enBounds = localization["PreviewBounds"];
        var enNoModelSelected = localization["PreviewNoModelSelected"];
        var enViewportInitFailed = localization["PreviewViewportInitFailed"];

        // Assert EN translations
        Assert.Equal("Model Preview Status", enPanelTitle);
        Assert.Equal("Cancel", enCancel);
        Assert.Equal("Preview", enButton);
        Assert.Equal("Select a mesh file and click Preview to view the 3D model.", enPlaceholder);
        Assert.Equal("Vertices: {0} | Triangles: {1} | Materials: {2}", enMeshSummary);
        Assert.Equal("Bounds: {0}", enBounds);
        Assert.Equal("No model selected for preview.", enNoModelSelected);
        Assert.Equal("OpenGL Viewport Initialization Failed", enViewportInitFailed);
    }

    [Fact]
    public void Preview_ComputedProperties_ShouldReturnCorrectValues()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var localization = new LocalizationService();
        localization.SetCulture("en-US");

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization);

        // 1. Initial State (No Model Selected, No Preview Result)
        Assert.Null(viewModel.SelectedPreviewModelReference);
        Assert.Equal(string.Empty, viewModel.PreviewModelDisplayName);
        Assert.False(viewModel.HasPreviewBounds);
        Assert.Equal(string.Empty, viewModel.PreviewBoundsSummary);
        Assert.Equal(string.Empty, viewModel.PreviewMeshSummaryText);

        // 2. Model Reference Selected (But Result is still null)
        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);
        viewModel.SelectedPreviewModelReference = modelRef;
        Assert.Equal("mesh.o3d", viewModel.PreviewModelDisplayName);
        Assert.False(viewModel.HasPreviewBounds);
        Assert.Equal(string.Empty, viewModel.PreviewBoundsSummary);
        Assert.Equal(string.Empty, viewModel.PreviewMeshSummaryText);

        // 3. Preview Result is set (With Bounds and MeshData)
        var meshData = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new(), new() },
            Triangles = new List<O3dTriangle> { new() },
            MaterialSlots = new List<O3dMaterialSlot> 
            { 
                new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "tex0.png" },
                new O3dMaterialSlot { MaterialName = "", TextureReference = null }
            }
        };

        var bounds = new MeshBounds
        {
            Min = new PreviewVector3D { X = -1.0f, Y = -2.0f, Z = -3.0f },
            Max = new PreviewVector3D { X = 1.0f, Y = 2.0f, Z = 3.0f },
            Size = new PreviewVector3D { X = 2.0f, Y = 4.0f, Z = 6.0f }
        };

        var result = new AssetPreviewResult
        {
            Status = AssetPreviewStatus.Success,
            MeshData = meshData,
            Bounds = bounds
        };

        viewModel.PreviewResult = result;

        Assert.True(viewModel.HasPreviewBounds);
        Assert.Contains("Vertices: 2", viewModel.PreviewMeshSummaryText);
        Assert.Contains("Triangles: 1", viewModel.PreviewMeshSummaryText);
        Assert.Contains("Materials: 2", viewModel.PreviewMeshSummaryText);

        Assert.True(viewModel.HasPreviewMaterials);
        Assert.Equal(2, viewModel.PreviewMaterials.Count);
        Assert.Equal("Mat0", viewModel.PreviewMaterials[0].MaterialName);
        Assert.Equal("tex0.png", viewModel.PreviewMaterials[0].TextureReference);
        Assert.Equal("Material 1", viewModel.PreviewMaterials[1].MaterialName);
        Assert.Equal("(No texture)", viewModel.PreviewMaterials[1].TextureReference);

        var minX = (-1.0f).ToString("F3");
        var minY = (-2.0f).ToString("F3");
        var minZ = (-3.0f).ToString("F3");
        var maxX = (1.0f).ToString("F3");
        var maxY = (2.0f).ToString("F3");
        var maxZ = (3.0f).ToString("F3");
        var sizeX = (2.0f).ToString("F3");
        var sizeY = (4.0f).ToString("F3");
        var sizeZ = (6.0f).ToString("F3");
        var expectedSummary = $"Bounds: Min ({minX}, {minY}, {minZ}) Max ({maxX}, {maxY}, {maxZ}) Size ({sizeX}, {sizeY}, {sizeZ})";
        Assert.Equal(expectedSummary, viewModel.PreviewBoundsSummary);
    }

    [Fact]
    public async Task Preview_RendererHost_MeshPropagation_ShouldWorkCorrectly()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var fakeRendererHost = new NullRendererHost();
        
        var meshData = new O3dMeshData();
        var expectedResult = new AssetPreviewResult
        {
            Status = AssetPreviewStatus.Success,
            MeshData = meshData
        };
        
        fakeLoader.LoadFunc = (req, token) => Task.FromResult(expectedResult);

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader,
            rendererHost: fakeRendererHost);

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // Act - Trigger load preview
        viewModel.SelectedPreviewModelReference = modelRef;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null)
        {
            await execution;
        }

        // Assert - Mesh is set on host
        Assert.Equal(meshData, fakeRendererHost.CurrentMesh);

        // Act - Reset SelectedPreviewModelReference to null
        viewModel.SelectedPreviewModelReference = null;

        // Assert - Mesh is cleared on host
        Assert.Null(fakeRendererHost.CurrentMesh);
    }

    private class FakeAssetPreviewLoader : IAssetPreviewLoader
    {
        public Func<AssetPreviewRequest, CancellationToken, Task<AssetPreviewResult>>? LoadFunc { get; set; }
        public AssetPreviewRequest? LastRequest { get; private set; }

        public Task<AssetPreviewResult> LoadAsync(AssetPreviewRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (LoadFunc != null)
            {
                return LoadFunc(request, cancellationToken);
            }
            return Task.FromResult(new AssetPreviewResult { Status = AssetPreviewStatus.Idle });
        }
    }

    [Fact]
    public void Preview_CameraState_InitializesCorrectly()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService());

        // Assert
        Assert.Equal(45f, viewModel.CameraYaw);
        Assert.Equal(-30f, viewModel.CameraPitch);
        Assert.Equal(5f, viewModel.CameraDistance);
    }

    [Fact]
    public void Preview_CameraState_PropagatesToRendererHost()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeRendererHost = new NullRendererHost();
        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            rendererHost: fakeRendererHost);

        // Act & Assert
        viewModel.CameraYaw = 90f;
        Assert.NotNull(fakeRendererHost.CameraState);
        Assert.Equal(90f, fakeRendererHost.CameraState.Yaw);

        viewModel.CameraPitch = -15f;
        Assert.Equal(-15f, fakeRendererHost.CameraState.Pitch);

        viewModel.CameraDistance = 10f;
        Assert.Equal(10f, fakeRendererHost.CameraState.Distance);
    }

    [Fact]
    public void Preview_ResetCameraCommand_RestoresDefaultState()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeRendererHost = new NullRendererHost();
        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            rendererHost: fakeRendererHost);

        viewModel.CameraYaw = 90f;
        viewModel.CameraPitch = -15f;
        viewModel.CameraDistance = 10f;

        // Act
        viewModel.ResetCameraCommand.Execute(null);

        // Assert
        Assert.Equal(45f, viewModel.CameraYaw);
        Assert.Equal(-30f, viewModel.CameraPitch);
        Assert.Equal(5f, viewModel.CameraDistance);
        
        Assert.NotNull(fakeRendererHost.CameraState);
        Assert.Equal(45f, fakeRendererHost.CameraState.Yaw);
        Assert.Equal(-30f, fakeRendererHost.CameraState.Pitch);
        Assert.Equal(5f, fakeRendererHost.CameraState.Distance);
    }

    [Fact]
    public void Preview_CameraProperties_ClampCorrectly()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService());

        // Act & Assert (Pitch clamp [-89, 89])
        viewModel.CameraPitch = 100f;
        Assert.Equal(89f, viewModel.CameraPitch);

        viewModel.CameraPitch = -100f;
        Assert.Equal(-89f, viewModel.CameraPitch);

        // Act & Assert (Distance clamp [0.5, 50])
        viewModel.CameraDistance = 60f;
        Assert.Equal(50f, viewModel.CameraDistance);

        viewModel.CameraDistance = 0.1f;
        Assert.Equal(0.5f, viewModel.CameraDistance);
    }

    [Fact]
    public void Preview_CameraCommands_ExecuteCorrectly()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService());

        viewModel.CameraYaw = 45f;
        viewModel.CameraPitch = 0f;
        viewModel.CameraDistance = 5f;

        // Act - Yaw commands
        viewModel.OrbitYawLeftCommand.Execute(null);
        Assert.Equal(30f, viewModel.CameraYaw);

        viewModel.OrbitYawRightCommand.Execute(null);
        Assert.Equal(45f, viewModel.CameraYaw);

        // Act - Pitch commands
        viewModel.OrbitPitchUpCommand.Execute(null);
        Assert.Equal(15f, viewModel.CameraPitch);

        viewModel.OrbitPitchDownCommand.Execute(null);
        Assert.Equal(0f, viewModel.CameraPitch);

        // Act - Zoom commands
        viewModel.ZoomInCommand.Execute(null);
        Assert.Equal(4f, viewModel.CameraDistance);

        viewModel.ZoomOutCommand.Execute(null);
        Assert.Equal(5f, viewModel.CameraDistance);
    }

    [Fact]
    public void Preview_LocalizationKeys_ArePresentInBothLanguages()
    {
        // Arrange
        var localization = new LocalizationService();

        var keys = new[]
        {
            "CamYawLeft", "CamYawRight", "CamPitchUp", "CamPitchDown", "CamZoomIn", "CamZoomOut", "CamReset"
        };

        // Act & Assert - Turkish
        localization.SetCulture("tr-TR");
        foreach (var key in keys)
        {
            var value = localization[key];
            Assert.NotEqual(key, value);
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        // Act & Assert - English
        localization.SetCulture("en-US");
        foreach (var key in keys)
        {
            var value = localization[key];
            Assert.NotEqual(key, value);
            Assert.False(string.IsNullOrWhiteSpace(value));
        }
    }

    private class FakeMaterialTextureBindingService : IMaterialTextureBindingService
    {
        public Func<O3dMeshData, string, string, CancellationToken, Task<IReadOnlyList<MaterialTextureBinding>>>? OnBindAsync { get; set; }

        public Task<IReadOnlyList<MaterialTextureBinding>> BindAsync(O3dMeshData meshData, string modelFilePath, string sceneryObjectsRoot, CancellationToken cancellationToken = default)
        {
            return OnBindAsync?.Invoke(meshData, modelFilePath, sceneryObjectsRoot, cancellationToken)
                ?? Task.FromResult<IReadOnlyList<MaterialTextureBinding>>(Array.Empty<MaterialTextureBinding>());
        }
    }

    [Fact]
    public async Task Preview_LoadSuccess_PopulatesPreviewTextureBindings()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader
        {
            LoadFunc = (req, token) => Task.FromResult(new AssetPreviewResult
            {
                Status = AssetPreviewStatus.Success,
                MeshData = new O3dMeshData
                {
                    MaterialSlots = [new O3dMaterialSlot { MaterialName = "Mat", TextureReference = "tex.png" }]
                }
            })
        };

        var mockBindings = new List<MaterialTextureBinding>
        {
            new MaterialTextureBinding { MaterialIndex = 0, MaterialName = "Mat", TextureReference = "tex.png", Status = TextureBindingStatus.Bound }
        };

        var fakeBindingService = new FakeMaterialTextureBindingService
        {
            OnBindAsync = (mesh, path, root, token) => Task.FromResult<IReadOnlyList<MaterialTextureBinding>>(mockBindings)
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader,
            materialTextureBindingService: fakeBindingService);

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // Act
        viewModel.SelectedPreviewModelReference = modelRef;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        // Assert
        Assert.NotNull(viewModel.PreviewTextureBindings);
        Assert.Single(viewModel.PreviewTextureBindings);
        Assert.Equal(TextureBindingStatus.Bound, viewModel.PreviewTextureBindings[0].Status);
    }

    [Fact]
    public async Task Preview_ResetOrCancel_ClearsPreviewTextureBindings()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader
        {
            LoadFunc = (req, token) => Task.FromResult(new AssetPreviewResult
            {
                Status = AssetPreviewStatus.Success,
                MeshData = new O3dMeshData { MaterialSlots = [new O3dMaterialSlot { MaterialName = "Mat", TextureReference = "tex.png" }] }
            })
        };

        var fakeBindingService = new FakeMaterialTextureBindingService
        {
            OnBindAsync = (mesh, path, root, token) => Task.FromResult<IReadOnlyList<MaterialTextureBinding>>([
                new MaterialTextureBinding { MaterialIndex = 0, Status = TextureBindingStatus.Bound }
            ])
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader,
            materialTextureBindingService: fakeBindingService);

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // 1. Load success
        viewModel.SelectedPreviewModelReference = modelRef;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;
        Assert.NotNull(viewModel.PreviewTextureBindings);

        // 2. Act - Reset selection
        viewModel.SelectedPreviewModelReference = null;
        Assert.Null(viewModel.PreviewTextureBindings);

        // 3. Act - Reload and then Cancel
        viewModel.SelectedPreviewModelReference = modelRef;
        execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;
        Assert.NotNull(viewModel.PreviewTextureBindings);

        viewModel.CancelPreviewCommand.Execute(null);
        Assert.Null(viewModel.PreviewTextureBindings);
    }

    [Fact]
    public async Task Preview_BindingServiceFails_PreviewRemainsSuccess()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader
        {
            LoadFunc = (req, token) => Task.FromResult(new AssetPreviewResult
            {
                Status = AssetPreviewStatus.Success,
                MeshData = new O3dMeshData { MaterialSlots = [new O3dMaterialSlot { MaterialName = "Mat", TextureReference = "tex.png" }] }
            })
        };

        var fakeBindingService = new FakeMaterialTextureBindingService
        {
            OnBindAsync = (mesh, path, root, token) => throw new Exception("Simulated loader/binding failure")
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader,
            materialTextureBindingService: fakeBindingService);

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // Act
        viewModel.SelectedPreviewModelReference = modelRef;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        // Assert - Main preview status is still success
        Assert.Equal(AssetPreviewStatus.Success, viewModel.PreviewStatus);
        Assert.Null(viewModel.PreviewTextureBindings); // Bindings are empty/null due to error
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("/users/omsi", "/users/omsi/Sceneryobjects")]
    [InlineData("/users/omsi/", "/users/omsi/Sceneryobjects")]
    [InlineData("/users/omsi/Sceneryobjects", "/users/omsi/Sceneryobjects")]
    [InlineData("/users/omsi/Sceneryobjects/", "/users/omsi/Sceneryobjects")]
    [InlineData("/users/omsi/SCENERYOBJECTS", "/users/omsi/SCENERYOBJECTS")]
    [InlineData("/users/omsi/FooSceneryobjects", "/users/omsi/FooSceneryobjects/Sceneryobjects")]
    [InlineData("/users/omsi/SceneryobjectsBackup", "/users/omsi/SceneryobjectsBackup/Sceneryobjects")]
    public void GetSceneryObjectsRoot_ResolvesPathCorrectly(string? input, string expected)
    {
        string actual = MainWindowViewModel.GetSceneryObjectsRoot(input);
        
        string normActual = actual.Replace('\\', '/').TrimEnd('/');
        string normExpected = expected.Replace('\\', '/').TrimEnd('/');
        Assert.Equal(normExpected, normActual);
    }

    [Fact]
    public async Task Preview_BindingCancellation_RethrowsAndLeavesPreviewCancelled()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader
        {
            LoadFunc = (req, token) => Task.FromResult(new AssetPreviewResult
            {
                Status = AssetPreviewStatus.Success,
                MeshData = new O3dMeshData { MaterialSlots = [new O3dMaterialSlot { MaterialName = "Mat", TextureReference = "tex.png" }] }
            })
        };

        var fakeBindingService = new FakeMaterialTextureBindingService
        {
            OnBindAsync = (mesh, path, root, token) => throw new OperationCanceledException(token)
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader,
            materialTextureBindingService: fakeBindingService);

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // Act
        viewModel.SelectedPreviewModelReference = modelRef;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        // Assert - Main preview status should be Cancelled
        Assert.Equal(AssetPreviewStatus.Cancelled, viewModel.PreviewStatus);
        Assert.Null(viewModel.PreviewTextureBindings);
    }

    [Fact]
    public async Task Preview_CancelCommandDuringBinding_PreventsSuccessOverwrite()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader
        {
            LoadFunc = (req, token) => Task.FromResult(new AssetPreviewResult
            {
                Status = AssetPreviewStatus.Success,
                MeshData = new O3dMeshData { MaterialSlots = [new O3dMaterialSlot { MaterialName = "Mat", TextureReference = "tex.png" }] }
            })
        };

        MainWindowViewModel? viewModel = null;

        var fakeBindingService = new FakeMaterialTextureBindingService
        {
            OnBindAsync = (mesh, path, root, token) =>
            {
                // Cancel preview VM command during the async binding call
                viewModel?.CancelPreviewCommand.Execute(null);
                
                IReadOnlyList<MaterialTextureBinding> list = new List<MaterialTextureBinding>
                {
                    new MaterialTextureBinding { MaterialIndex = 0, Status = TextureBindingStatus.Bound }
                };
                return Task.FromResult(list);
            }
        };

        viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader,
            materialTextureBindingService: fakeBindingService);

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // Act
        viewModel.SelectedPreviewModelReference = modelRef;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        // Assert - Should remain Cancelled, and bindings must remain null
        Assert.Equal(AssetPreviewStatus.Cancelled, viewModel.PreviewStatus);
        Assert.Null(viewModel.PreviewTextureBindings);
    }

    [Fact]
    public void UpdatePreviewMaterials_WithVariousBindings_MapsPropertiesCorrectly()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var localization = new LocalizationService();

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            previewLoader: fakeLoader,
            materialTextureBindingService: null);

        var mesh = new O3dMeshData
        {
            MaterialSlots = new List<O3dMaterialSlot>
            {
                new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "tex0.png" },
                new O3dMaterialSlot { MaterialName = "Mat1", TextureReference = "tex1.png" },
                new O3dMaterialSlot { MaterialName = "Mat2", TextureReference = "tex2.png" },
                new O3dMaterialSlot { MaterialName = "Mat3", TextureReference = "tex3.png" },
            }
        };

        var previewResult = new AssetPreviewResult { MeshData = mesh, Status = AssetPreviewStatus.Success };

        // 1. Assert null bindings case
        viewModel.PreviewResult = previewResult; // Triggers UpdatePreviewMaterials
        Assert.Equal(4, viewModel.PreviewMaterials.Count);
        string notBoundText = localization["MaterialNotBound"];
        Assert.All(viewModel.PreviewMaterials, m => Assert.Equal(notBoundText, m.BindingStatus));

        // 2. Arrange bindings with varying statuses
        var image = new TextureImageData { Width = 32, Height = 32, PixelsRgba32 = new byte[32 * 32 * 4] };
        var bindings = new List<MaterialTextureBinding>
        {
            new MaterialTextureBinding
            {
                MaterialIndex = 0,
                Status = TextureBindingStatus.Bound,
                Image = image,
                ResolvedTexture = new OmsiTextureReference
                {
                    TexturePath = "tex0.png",
                    ResolvedPath = "/path/to/tex0.png",
                    Exists = true,
                    ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
                }
            },
            new MaterialTextureBinding
            {
                MaterialIndex = 1,
                Status = TextureBindingStatus.Missing,
                Diagnostics = new List<O3dDiagnostic> { new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Warning, Message = "Doku yok" } }
            },
            new MaterialTextureBinding
            {
                MaterialIndex = 2,
                Status = TextureBindingStatus.Unsupported,
                Diagnostics = new List<O3dDiagnostic> { new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Message = "Desteklenmiyor" } }
            }
        };

        // Act
        viewModel.PreviewTextureBindings = bindings; // Triggers UpdatePreviewMaterials

        // Assert - Slot 0 (Bound)
        var item0 = viewModel.PreviewMaterials[0];
        Assert.Equal("Bound", item0.BindingStatus);
        Assert.Equal("32x32", item0.ImageSizeText);
        Assert.Equal("/path/to/tex0.png", item0.ResolvedPath);
        Assert.False(item0.HasDiagnostics);

        // Assert - Slot 1 (Missing)
        var item1 = viewModel.PreviewMaterials[1];
        Assert.Equal("Missing", item1.BindingStatus);
        Assert.True(item1.HasDiagnostics);
        Assert.Equal("Doku yok", item1.DiagnosticsText);

        // Assert - Slot 2 (Unsupported)
        var item2 = viewModel.PreviewMaterials[2];
        Assert.Equal("Unsupported", item2.BindingStatus);
        Assert.True(item2.HasDiagnostics);
        Assert.Equal("Desteklenmiyor", item2.DiagnosticsText);

        // Assert - Slot 3 (Unbound)
        var item3 = viewModel.PreviewMaterials[3];
        Assert.Equal(notBoundText, item3.BindingStatus);
    }

    [Fact]
    public async Task SelectedAsset_Changed_AutoLoadsFirstResolvedModelReference()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var localization = new LocalizationService();

        var resolvedModel = new OmsiModelReference("mesh_resolved.o3d", "/path/mesh_resolved.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);
        var unresolvedModel = new OmsiModelReference("mesh_unresolved.o3d", "/path/mesh_unresolved.o3d", false, OmsiModelReferenceResolutionStatus.Missing);

        var asset = new OmsiAsset
        {
            RelativePath = "scenery/object.sco",
            ModelReferences = new List<OmsiModelReference>
            {
                unresolvedModel,
                resolvedModel
            }
        };

        var meshData = new O3dMeshData();
        var previewResult = new AssetPreviewResult { MeshData = meshData, Status = AssetPreviewStatus.Success };
        fakeLoader.LoadFunc = (req, ct) => Task.FromResult(previewResult);

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            previewLoader: fakeLoader,
            materialTextureBindingService: null);

        // Act
        viewModel.SelectedAsset = asset;

        // Assert - should automatically select the first resolved model reference and load it
        Assert.Same(resolvedModel, viewModel.SelectedPreviewModelReference);

        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        Assert.Equal(AssetPreviewStatus.Success, viewModel.PreviewStatus);
        Assert.Same(previewResult, viewModel.PreviewResult);
    }

    [Fact]
    public void SelectedAsset_Changed_ClearsPreviewIfNoResolvedModelReference()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var localization = new LocalizationService();

        var unresolvedModel = new OmsiModelReference("mesh_unresolved.o3d", "/path/mesh_unresolved.o3d", false, OmsiModelReferenceResolutionStatus.Missing);

        var asset = new OmsiAsset
        {
            RelativePath = "scenery/object.sco",
            ModelReferences = new List<OmsiModelReference> { unresolvedModel }
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            previewLoader: fakeLoader,
            materialTextureBindingService: null);

        // Setup dummy pre-existing preview state to verify it gets cleared
        viewModel.PreviewResult = new AssetPreviewResult { MeshData = new O3dMeshData(), Status = AssetPreviewStatus.Success };
        viewModel.PreviewStatus = AssetPreviewStatus.Success;

        // Act
        viewModel.SelectedAsset = asset;

        // Assert
        Assert.Null(viewModel.SelectedPreviewModelReference);
        Assert.Equal(AssetPreviewStatus.Idle, viewModel.PreviewStatus);
        Assert.Null(viewModel.PreviewResult);
    }

    [Fact]
    public void SelectedAsset_Changed_SkipsResolvedNonO3dAndSelectsResolvedO3d()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var localization = new LocalizationService();

        var resolvedNonO3d = new OmsiModelReference("mesh_resolved.obj", "/path/mesh_resolved.obj", true, OmsiModelReferenceResolutionStatus.Resolved);
        var resolvedO3d = new OmsiModelReference("mesh_resolved.o3d", "/path/mesh_resolved.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        var asset = new OmsiAsset
        {
            RelativePath = "scenery/object.sco",
            ModelReferences = new List<OmsiModelReference> { resolvedNonO3d, resolvedO3d }
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            previewLoader: fakeLoader,
            materialTextureBindingService: null);

        // Act
        viewModel.SelectedAsset = asset;

        // Assert
        Assert.Same(resolvedO3d, viewModel.SelectedPreviewModelReference);
    }

    [Fact]
    public void SelectedAsset_Changed_SelectsResolvedUppercaseO3d()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var localization = new LocalizationService();

        var resolvedUppercaseO3d = new OmsiModelReference("mesh_resolved.O3D", "/path/mesh_resolved.O3D", true, OmsiModelReferenceResolutionStatus.Resolved);

        var asset = new OmsiAsset
        {
            RelativePath = "scenery/object.sco",
            ModelReferences = new List<OmsiModelReference> { resolvedUppercaseO3d }
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            previewLoader: fakeLoader,
            materialTextureBindingService: null);

        // Act
        viewModel.SelectedAsset = asset;

        // Assert
        Assert.Same(resolvedUppercaseO3d, viewModel.SelectedPreviewModelReference);
    }

    [Fact]
    public void SelectedAsset_Changed_ClearsPreviewIfOnlyResolvedNonO3dExists()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var localization = new LocalizationService();

        var resolvedNonO3d = new OmsiModelReference("mesh_resolved.obj", "/path/mesh_resolved.obj", true, OmsiModelReferenceResolutionStatus.Resolved);

        var asset = new OmsiAsset
        {
            RelativePath = "scenery/object.sco",
            ModelReferences = new List<OmsiModelReference> { resolvedNonO3d }
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            previewLoader: fakeLoader,
            materialTextureBindingService: null);

        // Setup dummy pre-existing preview state
        viewModel.PreviewResult = new AssetPreviewResult { MeshData = new O3dMeshData(), Status = AssetPreviewStatus.Success };
        viewModel.PreviewStatus = AssetPreviewStatus.Success;

        // Act
        viewModel.SelectedAsset = asset;

        // Assert
        Assert.Null(viewModel.SelectedPreviewModelReference);
        Assert.Equal(AssetPreviewStatus.Idle, viewModel.PreviewStatus);
        Assert.Null(viewModel.PreviewResult);
    }

    [Fact]
    public async Task SelectedAsset_Changed_AutoPreviewLoadsAllResolvedO3dModelReferences()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var localization = new LocalizationService();

        var resolvedModel1 = new OmsiModelReference("mesh1.o3d", "/path/mesh1.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);
        var resolvedModel2 = new OmsiModelReference("mesh2.o3d", "/path/mesh2.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);
        var unresolvedModel = new OmsiModelReference("mesh_unresolved.o3d", "/path/mesh_unresolved.o3d", false, OmsiModelReferenceResolutionStatus.Missing);

        var asset = new OmsiAsset
        {
            RelativePath = "scenery/object.sco",
            ModelReferences = new List<OmsiModelReference>
            {
                resolvedModel1,
                unresolvedModel,
                resolvedModel2
            }
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            previewLoader: fakeLoader,
            materialTextureBindingService: null);

        // Act
        viewModel.SelectedAsset = asset;

        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        // Assert - auto preview request should contain all resolved .o3d paths
        Assert.NotNull(fakeLoader.LastRequest);
        Assert.Equal(2, fakeLoader.LastRequest.ModelPaths.Count);
        Assert.Contains("/path/mesh1.o3d", fakeLoader.LastRequest.ModelPaths);
        Assert.Contains("/path/mesh2.o3d", fakeLoader.LastRequest.ModelPaths);
        Assert.DoesNotContain("/path/mesh_unresolved.o3d", fakeLoader.LastRequest.ModelPaths);
    }

    [Fact]
    public async Task LoadPreviewCommand_WithManualParameter_LoadsOnlySingleMesh()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var localization = new LocalizationService();

        var resolvedModel1 = new OmsiModelReference("mesh1.o3d", "/path/mesh1.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);
        var resolvedModel2 = new OmsiModelReference("mesh2.o3d", "/path/mesh2.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        var asset = new OmsiAsset
        {
            RelativePath = "scenery/object.sco",
            ModelReferences = new List<OmsiModelReference> { resolvedModel1, resolvedModel2 }
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            previewLoader: fakeLoader,
            materialTextureBindingService: null);

        // Act
        viewModel.SelectedAsset = asset;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        // Reset and trigger single manual mesh load
        viewModel.LoadPreviewCommand.Execute(resolvedModel2);
        execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        // Assert - should load ONLY resolvedModel2
        Assert.NotNull(fakeLoader.LastRequest);
        Assert.Single(fakeLoader.LastRequest.ModelPaths);
        Assert.Equal("/path/mesh2.o3d", fakeLoader.LastRequest.ModelPaths[0]);
    }

    [Fact]
    public async Task SelectedAsset_Changed_AutoPreviewPrioritizesResolvedO3dOverDirectX()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var localization = new LocalizationService();

        var resolvedModelX = new OmsiModelReference("mesh1.x", "/path/mesh1.x", true, OmsiModelReferenceResolutionStatus.Resolved);
        var resolvedModelO3d = new OmsiModelReference("mesh2.o3d", "/path/mesh2.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        var asset = new OmsiAsset
        {
            RelativePath = "scenery/object.sco",
            ModelReferences = new List<OmsiModelReference> { resolvedModelX, resolvedModelO3d }
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            previewLoader: fakeLoader,
            materialTextureBindingService: null);

        // Act
        viewModel.SelectedAsset = asset;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        // Assert - should select and prioritize resolved O3D reference
        Assert.Same(resolvedModelO3d, viewModel.SelectedPreviewModelReference);
    }

    [Fact]
    public async Task SelectedAsset_Changed_AutoPreviewSelectsDirectXIfNoO3dExists()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var localization = new LocalizationService();

        var resolvedModelX = new OmsiModelReference("mesh1.x", "/path/mesh1.x", true, OmsiModelReferenceResolutionStatus.Resolved);

        var asset = new OmsiAsset
        {
            RelativePath = "scenery/object.sco",
            ModelReferences = new List<OmsiModelReference> { resolvedModelX }
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            previewLoader: fakeLoader,
            materialTextureBindingService: null);

        // Act
        viewModel.SelectedAsset = asset;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        // Assert - should select resolved .x reference as candidate
        Assert.Same(resolvedModelX, viewModel.SelectedPreviewModelReference);
    }

    [Fact]
    public async Task SelectedAsset_Changed_SelectsUppercaseXFormat()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        var localization = new LocalizationService();

        var resolvedModelUppercaseX = new OmsiModelReference("mesh1.X", "/path/mesh1.X", true, OmsiModelReferenceResolutionStatus.Resolved);

        var asset = new OmsiAsset
        {
            RelativePath = "scenery/object.sco",
            ModelReferences = new List<OmsiModelReference> { resolvedModelUppercaseX }
        };

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), localization,
            previewLoader: fakeLoader,
            materialTextureBindingService: null);

        // Act
        viewModel.SelectedAsset = asset;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null) await execution;

        // Assert - case-insensitive check selects .X reference
        Assert.Same(resolvedModelUppercaseX, viewModel.SelectedPreviewModelReference);
    }

    [Fact]
    public async Task Preview_TextureBindingDiagnosticsAndGuardrails_MergeIntoPreviewDiagnostics()
    {
        // Arrange
        var fakeScanner = new FakeAssetScanner();
        var fakeFolderPicker = new FakeFolderPickerService();
        var appSettings = new FakeAppSettingsService();
        var fakeLoader = new FakeAssetPreviewLoader();
        
        var expectedResult = new AssetPreviewResult
        {
            Status = AssetPreviewStatus.Success,
            MeshData = new O3dMeshData
            {
                Vertices = new List<O3dVertex> { new() { X = 0, Y = 0, Z = 0 } },
                Triangles = new List<O3dTriangle> { new() { V0 = 0, V1 = 0, V2 = 0, MaterialSlotIndex = 0 } },
                MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "Mat0", TextureReference = "tex0.png" } }
            },
            Diagnostics = new List<O3dDiagnostic>
            {
                new() { Severity = O3dDiagnosticSeverity.Warning, Code = O3dDiagnosticCode.SafetyLimitExceeded, Message = "Vertices limit warning" }
            }
        };
        
        fakeLoader.LoadFunc = (req, token) => Task.FromResult(expectedResult);

        var viewModel = new MainWindowViewModel(
            fakeScanner, fakeFolderPicker, appSettings,
            new FakeClipboardService(), new FakeFileLauncherService(), new LocalizationService(),
            previewLoader: fakeLoader,
            materialTextureBindingService: new VMDiagFakeBindingService());

        var modelRef = new OmsiModelReference("mesh.o3d", "/path/to/mesh.o3d", true, OmsiModelReferenceResolutionStatus.Resolved);

        // Act
        viewModel.SelectedPreviewModelReference = modelRef;
        var execution = ((IAsyncRelayCommand)viewModel.LoadPreviewCommand).ExecutionTask;
        if (execution != null)
        {
            await execution;
        }

        // Assert
        Assert.True(viewModel.HasPreviewDiagnostics);
        // Expecting 2 diagnostics: 1 from loader, 1 from binding service
        Assert.Equal(2, viewModel.PreviewDiagnostics.Count);
        Assert.Contains(viewModel.PreviewDiagnostics, d => d.Message == "Vertices limit warning");
        Assert.Contains(viewModel.PreviewDiagnostics, d => d.Message == "Texture budget exceeded");
    }

    private class VMDiagFakeBindingService : IMaterialTextureBindingService
    {
        public Task<IReadOnlyList<MaterialTextureBinding>> BindAsync(O3dMeshData meshData, string modelFilePath, string sceneryObjectsRoot, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MaterialTextureBinding> result = new List<MaterialTextureBinding>
            {
                new()
                {
                    MaterialIndex = 0,
                    MaterialName = "Mat0",
                    TextureReference = "tex0.png",
                    Status = TextureBindingStatus.TooLarge,
                    Diagnostics = new List<O3dDiagnostic>
                    {
                        new() { Severity = O3dDiagnosticSeverity.Warning, Code = O3dDiagnosticCode.SafetyLimitExceeded, Message = "Texture budget exceeded" }
                    }
                }
            };
            return Task.FromResult(result);
        }
    }
}
