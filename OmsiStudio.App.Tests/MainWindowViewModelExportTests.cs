using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Scanning;
using OmsiStudio.Core.Conversion;
using OmsiStudio.Core.Services;
using OmsiStudio.App.Services;
using OmsiStudio.App.ViewModels;

namespace OmsiStudio.App.Tests;

public class MainWindowViewModelExportTests
{
    static MainWindowViewModelExportTests()
    {
        MainWindowViewModel.IsTestMode = true;
    }
    private class FakeFolderPickerService : IFolderPickerService
    {
        public string? PresetPath { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public bool WasCalled { get; private set; }

        public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }
            return Task.FromResult(PresetPath);
        }
    }

    private class FakeAssetConversionService : IAssetConversionService
    {
        public ConversionResult? PresetResult { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public bool WasCalled { get; private set; }
        public ConversionRequest? CapturedRequest { get; private set; }

        public Task<ConversionResult> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            CapturedRequest = request;

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(PresetResult ?? new ConversionResult { Status = ConversionStatus.Succeeded });
        }
    }

    private class FakeAssetScanner : IOmsiAssetScanner
    {
        public IAsyncEnumerable<OmsiAsset> ScanDirectoryAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<OmsiScanResult> ScanAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<OmsiScanResult> ScanAsync(string rootDirectory, IProgress<OmsiScanProgress>? progress, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private class FakeAppSettingsService : IAppSettingsService
    {
        public Task<string?> GetLastOmsiRootAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task SaveLastOmsiRootAsync(string rootDirectory, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetLastLanguageAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task SaveLastLanguageAsync(string cultureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeClipboardService : IClipboardService
    {
        public Task SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeFileLauncherService : IFileLauncherService
    {
        public Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeLocalizationService : ILocalizationService
    {
        public event EventHandler? CultureChanged
        {
            add { }
            remove { }
        }
        public string CurrentCulture => "en-US";
        public string this[string key] => key switch
        {
            "ExportSuccessFormat" => "Success: {0}",
            "ExportFailFormat" => "Failed: {0}",
            "ExportFolderPickFail" => "Folder pick failed: {0}",
            _ => key
        };
        public void SetCulture(string cultureName) { }
    }

    [Fact]
    public async Task ExportManifestCommand_WithNullSelectedAsset_ShouldBeNoOp()
    {
        // Arrange
        var folderPicker = new FakeFolderPickerService { PresetPath = "/output" };
        var conversionService = new FakeAssetConversionService();
        var viewModel = new MainWindowViewModel(
            new FakeAssetScanner(),
            folderPicker,
            new FakeAppSettingsService(),
            new FakeClipboardService(),
            new FakeFileLauncherService(),
            new FakeLocalizationService(),
            conversionService);

        viewModel.SelectedAsset = null;

        // Act
        await viewModel.ExportManifestCommand.ExecuteAsync(null);

        // Assert
        Assert.False(folderPicker.WasCalled);
        Assert.False(conversionService.WasCalled);
        Assert.Null(viewModel.ExportSuccessMessage);
        Assert.Null(viewModel.ExportErrorMessage);
        Assert.False(viewModel.HasExportStatus);
    }

    [Fact]
    public async Task ExportManifestCommand_WithCancelledFolderPicker_ShouldBeNoOp()
    {
        // Arrange
        var folderPicker = new FakeFolderPickerService { PresetPath = null }; // Cancelled
        var conversionService = new FakeAssetConversionService();
        var viewModel = new MainWindowViewModel(
            new FakeAssetScanner(),
            folderPicker,
            new FakeAppSettingsService(),
            new FakeClipboardService(),
            new FakeFileLauncherService(),
            new FakeLocalizationService(),
            conversionService);

        viewModel.SelectedAsset = new OmsiAsset { DisplayName = "Test Asset", SourceScoPath = "/path/test.sco" };

        // Act
        await viewModel.ExportManifestCommand.ExecuteAsync(null);

        // Assert
        Assert.True(folderPicker.WasCalled);
        Assert.False(conversionService.WasCalled);
        Assert.Null(viewModel.ExportSuccessMessage);
        Assert.Null(viewModel.ExportErrorMessage);
    }

    [Fact]
    public async Task ExportManifestCommand_WithSuccessfulConversion_SetsSuccessMessage()
    {
        // Arrange
        var folderPicker = new FakeFolderPickerService { PresetPath = "/output" };
        var conversionService = new FakeAssetConversionService
        {
            PresetResult = new ConversionResult
            {
                Status = ConversionStatus.Succeeded,
                OutputFiles = new List<string> { "/output/test_manifest.json" }
            }
        };

        var viewModel = new MainWindowViewModel(
            new FakeAssetScanner(),
            folderPicker,
            new FakeAppSettingsService(),
            new FakeClipboardService(),
            new FakeFileLauncherService(),
            new FakeLocalizationService(),
            conversionService);

        viewModel.SelectedAsset = new OmsiAsset { DisplayName = "Test Asset", SourceScoPath = "/path/test.sco" };

        // Act
        await viewModel.ExportManifestCommand.ExecuteAsync(null);

        // Assert
        Assert.True(folderPicker.WasCalled);
        Assert.True(conversionService.WasCalled);
        Assert.NotNull(conversionService.CapturedRequest);
        Assert.Equal(ConversionTargetFormat.ManifestOnly, conversionService.CapturedRequest.TargetFormat);
        Assert.Equal("/output", conversionService.CapturedRequest.TargetOutputDirectory);
        Assert.Equal(viewModel.SelectedAsset, conversionService.CapturedRequest.Asset);

        Assert.Equal("Success: /output/test_manifest.json", viewModel.ExportSuccessMessage);
        Assert.Null(viewModel.ExportErrorMessage);
        Assert.True(viewModel.HasExportSuccess);
        Assert.False(viewModel.HasExportError);
        Assert.True(viewModel.HasExportStatus);
    }

    [Fact]
    public async Task ExportManifestCommand_WithFailedConversion_SetsErrorMessage()
    {
        // Arrange
        var folderPicker = new FakeFolderPickerService { PresetPath = "/output" };
        var conversionService = new FakeAssetConversionService
        {
            PresetResult = new ConversionResult
            {
                Status = ConversionStatus.Failed,
                Errors = new List<string> { "Disk full or permission denied" }
            }
        };

        var viewModel = new MainWindowViewModel(
            new FakeAssetScanner(),
            folderPicker,
            new FakeAppSettingsService(),
            new FakeClipboardService(),
            new FakeFileLauncherService(),
            new FakeLocalizationService(),
            conversionService);

        viewModel.SelectedAsset = new OmsiAsset { DisplayName = "Test Asset", SourceScoPath = "/path/test.sco" };

        // Act
        await viewModel.ExportManifestCommand.ExecuteAsync(null);

        // Assert
        Assert.True(folderPicker.WasCalled);
        Assert.True(conversionService.WasCalled);
        Assert.Equal("Failed: Disk full or permission denied", viewModel.ExportErrorMessage);
        Assert.Null(viewModel.ExportSuccessMessage);
        Assert.True(viewModel.HasExportError);
        Assert.False(viewModel.HasExportSuccess);
        Assert.True(viewModel.HasExportStatus);
    }

    [Fact]
    public async Task ExportManifestCommand_WithFolderPickException_SetsErrorMessage()
    {
        // Arrange
        var folderPicker = new FakeFolderPickerService { ExceptionToThrow = new Exception("Device disconnected") };
        var conversionService = new FakeAssetConversionService();
        var viewModel = new MainWindowViewModel(
            new FakeAssetScanner(),
            folderPicker,
            new FakeAppSettingsService(),
            new FakeClipboardService(),
            new FakeFileLauncherService(),
            new FakeLocalizationService(),
            conversionService);

        viewModel.SelectedAsset = new OmsiAsset { DisplayName = "Test Asset", SourceScoPath = "/path/test.sco" };

        // Act
        await viewModel.ExportManifestCommand.ExecuteAsync(null);

        // Assert
        Assert.True(folderPicker.WasCalled);
        Assert.False(conversionService.WasCalled);
        Assert.Equal("Folder pick failed: Device disconnected", viewModel.ExportErrorMessage);
        Assert.Null(viewModel.ExportSuccessMessage);
        Assert.True(viewModel.HasExportError);
    }

    [Fact]
    public async Task ExportManifestCommand_WithConversionException_SetsErrorMessage()
    {
        // Arrange
        var folderPicker = new FakeFolderPickerService { PresetPath = "/output" };
        var conversionService = new FakeAssetConversionService { ExceptionToThrow = new Exception("Write access denied") };
        var viewModel = new MainWindowViewModel(
            new FakeAssetScanner(),
            folderPicker,
            new FakeAppSettingsService(),
            new FakeClipboardService(),
            new FakeFileLauncherService(),
            new FakeLocalizationService(),
            conversionService);

        viewModel.SelectedAsset = new OmsiAsset { DisplayName = "Test Asset", SourceScoPath = "/path/test.sco" };

        // Act
        await viewModel.ExportManifestCommand.ExecuteAsync(null);

        // Assert
        Assert.True(folderPicker.WasCalled);
        Assert.True(conversionService.WasCalled);
        Assert.Equal("Failed: Write access denied", viewModel.ExportErrorMessage);
        Assert.Null(viewModel.ExportSuccessMessage);
        Assert.True(viewModel.HasExportError);
    }

    [Fact]
    public void OnSelectedAssetChanged_ClearsExportStatusMessages()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            new FakeAssetScanner(),
            new FakeFolderPickerService(),
            new FakeAppSettingsService(),
            new FakeClipboardService(),
            new FakeFileLauncherService(),
            new FakeLocalizationService(),
            new FakeAssetConversionService());

        viewModel.ExportSuccessMessage = "Success message";
        viewModel.ExportErrorMessage = "Error message";

        // Act
        viewModel.SelectedAsset = new OmsiAsset();

        // Assert
        Assert.Null(viewModel.ExportSuccessMessage);
        Assert.Null(viewModel.ExportErrorMessage);
        Assert.False(viewModel.HasExportStatus);
    }

    [Fact]
    public void ExportSuccessMessage_RaisesPropertyChanged_ForComputedProperties()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            new FakeAssetScanner(),
            new FakeFolderPickerService(),
            new FakeAppSettingsService(),
            new FakeClipboardService(),
            new FakeFileLauncherService(),
            new FakeLocalizationService(),
            new FakeAssetConversionService());

        var triggeredProperties = new List<string>();
        viewModel.PropertyChanged += (s, e) => { if (e.PropertyName != null) triggeredProperties.Add(e.PropertyName); };

        // Act
        viewModel.ExportSuccessMessage = "Success message";

        // Assert
        Assert.Contains(nameof(MainWindowViewModel.ExportSuccessMessage), triggeredProperties);
        Assert.Contains(nameof(MainWindowViewModel.HasExportSuccess), triggeredProperties);
        Assert.Contains(nameof(MainWindowViewModel.HasExportStatus), triggeredProperties);
    }

    [Fact]
    public void ExportErrorMessage_RaisesPropertyChanged_ForComputedProperties()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            new FakeAssetScanner(),
            new FakeFolderPickerService(),
            new FakeAppSettingsService(),
            new FakeClipboardService(),
            new FakeFileLauncherService(),
            new FakeLocalizationService(),
            new FakeAssetConversionService());

        var triggeredProperties = new List<string>();
        viewModel.PropertyChanged += (s, e) => { if (e.PropertyName != null) triggeredProperties.Add(e.PropertyName); };

        // Act
        viewModel.ExportErrorMessage = "Error message";

        // Assert
        Assert.Contains(nameof(MainWindowViewModel.ExportErrorMessage), triggeredProperties);
        Assert.Contains(nameof(MainWindowViewModel.HasExportError), triggeredProperties);
        Assert.Contains(nameof(MainWindowViewModel.HasExportStatus), triggeredProperties);
    }
}
