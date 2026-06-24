using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Scanning;
using OmsiStudio.Core.Services;
using OmsiStudio.OmsiFormat.Parser;
using OmsiStudio.OmsiFormat.Scanner;
using OmsiStudio.App.Services;
using OmsiStudio.Core.Conversion;
using OmsiStudio.Conversion;

namespace OmsiStudio.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IOmsiAssetScanner _scanner;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IClipboardService _clipboardService;
    private readonly IFileLauncherService _fileLauncherService;
    private readonly ILocalizationService _localizationService;
    private readonly IAssetConversionService _conversionService;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RootDirectoryDisplay))]
    private string? _rootDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoSelectionPrompt))]
    [NotifyPropertyChangedFor(nameof(ShowCancellationBanner))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(ShowNoSelectionPrompt))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCancellationBanner))]
    private string _scanProgressText = string.Empty;

    public bool ShowCancellationBanner => !IsScanning && !string.IsNullOrEmpty(ScanProgressText);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AssetCountDisplay))]
    private int _assetCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AssetCountDisplay))]
    private int _filteredAssetCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AssetCountDisplay))]
    private bool _hasActiveSearch;

    public string AssetCountDisplay => HasActiveSearch
        ? string.Format(_localizationService["ShowingOfFormat"], FilteredAssetCount, AssetCount)
        : string.Format(_localizationService["ObjectsFoundFormat"], AssetCount);

    public ILocalizationService L => _localizationService;

    public string TurkishButtonBackground => _localizationService.CurrentCulture == "tr-TR" ? "#6366f1" : "#1e293b";
    public string EnglishButtonBackground => _localizationService.CurrentCulture == "en-US" ? "#6366f1" : "#1e293b";

    public string RootDirectoryDisplay => !string.IsNullOrWhiteSpace(RootDirectory)
        ? RootDirectory
        : _localizationService["NoFolderSelected"];

    public string SelectedAssetDisplayName => SelectedAsset != null
        ? (string.IsNullOrEmpty(SelectedAsset.DisplayName) ? _localizationService["DefaultUntitled"] : SelectedAsset.DisplayName)
        : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoSelectionPrompt))]
    [NotifyPropertyChangedFor(nameof(HasSelectedAsset))]
    [NotifyPropertyChangedFor(nameof(SelectedAssetDisplayName))]
    private OmsiAsset? _selectedAsset;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoSelectionPrompt))]
    private bool _hasAssets;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEmptyState))]
    private bool _isEmptyState = true;

    public bool IsNotEmptyState => !IsEmptyState;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSelectedAsset => SelectedAsset != null;
    public bool ShowNoSelectionPrompt => !IsScanning && SelectedAsset == null && HasAssets && !HasError;

    public ObservableCollection<string> ScanWarnings { get; } = new();
    public ObservableCollection<string> ScanErrors { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScanMessages))]
    [NotifyPropertyChangedFor(nameof(HasScanWarnings))]
    [NotifyPropertyChangedFor(nameof(ScanWarningsDisplay))]
    private int _warningCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScanMessages))]
    [NotifyPropertyChangedFor(nameof(HasScanErrors))]
    [NotifyPropertyChangedFor(nameof(ScanErrorsDisplay))]
    private int _errorCount;

    public bool HasScanMessages => WarningCount > 0 || ErrorCount > 0;
    public bool HasScanWarnings => WarningCount > 0;
    public bool HasScanErrors => ErrorCount > 0;

    public string ScanErrorsDisplay => string.Format(_localizationService["ScanErrorFormat"], ErrorCount);
    public string ScanWarningsDisplay => string.Format(_localizationService["ScanWarningFormat"], WarningCount);

    private readonly List<OmsiAsset> _allAssets = new();
    public ObservableCollection<OmsiAsset> Assets { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportSuccess))]
    [NotifyPropertyChangedFor(nameof(HasExportStatus))]
    private string? _exportSuccessMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportError))]
    [NotifyPropertyChangedFor(nameof(HasExportStatus))]
    private string? _exportErrorMessage;

    public bool HasExportSuccess => !string.IsNullOrEmpty(ExportSuccessMessage);
    public bool HasExportError => !string.IsNullOrEmpty(ExportErrorMessage);
    public bool HasExportStatus => HasExportSuccess || HasExportError;

    partial void OnSelectedAssetChanged(OmsiAsset? value)
    {
        ExportSuccessMessage = null;
        ExportErrorMessage = null;
    }

    [ObservableProperty]
    private AssetGroupingMode _groupingMode = AssetGroupingMode.Folder;

    public ObservableCollection<AssetGroupViewModel> AssetGroups { get; } = new();

    public bool IsFolderGrouping
    {
        get => GroupingMode == AssetGroupingMode.Folder;
        set
        {
            if (value && GroupingMode != AssetGroupingMode.Folder)
            {
                GroupingMode = AssetGroupingMode.Folder;
                OnPropertyChanged(nameof(IsFolderGrouping));
                OnPropertyChanged(nameof(IsCategoryGrouping));
            }
        }
    }

    public bool IsCategoryGrouping
    {
        get => GroupingMode == AssetGroupingMode.Category;
        set
        {
            if (value && GroupingMode != AssetGroupingMode.Category)
            {
                GroupingMode = AssetGroupingMode.Category;
                OnPropertyChanged(nameof(IsFolderGrouping));
                OnPropertyChanged(nameof(IsCategoryGrouping));
            }
        }
    }

    partial void OnGroupingModeChanged(AssetGroupingMode value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(IsFolderGrouping));
        OnPropertyChanged(nameof(IsCategoryGrouping));
    }

    public MainWindowViewModel()
    {
        var parser = new ScoFileParser();
        _scanner = new OmsiAssetScanner(parser);
        _folderPickerService = new AvaloniaFolderPickerService();
        _appSettingsService = new JsonAppSettingsService();
        _clipboardService = new AvaloniaClipboardService();
        _fileLauncherService = new ProcessFileLauncherService();
        _localizationService = new LocalizationService();
        _conversionService = new AssetConversionService();

        _localizationService.CultureChanged += (s, e) => OnPropertyChanged(string.Empty);
    }

    public MainWindowViewModel(IOmsiAssetScanner scanner) 
        : this(scanner, new AvaloniaFolderPickerService(), new JsonAppSettingsService(), new AvaloniaClipboardService(), new ProcessFileLauncherService(), new LocalizationService())
    {
    }

    public MainWindowViewModel(IOmsiAssetScanner scanner, IFolderPickerService folderPickerService)
        : this(scanner, folderPickerService, new JsonAppSettingsService(), new AvaloniaClipboardService(), new ProcessFileLauncherService(), new LocalizationService())
    {
    }

    public MainWindowViewModel(
        IOmsiAssetScanner scanner, 
        IFolderPickerService folderPickerService, 
        IAppSettingsService appSettingsService)
        : this(scanner, folderPickerService, appSettingsService, new AvaloniaClipboardService(), new ProcessFileLauncherService(), new LocalizationService())
    {
    }

    public MainWindowViewModel(
        IOmsiAssetScanner scanner, 
        IFolderPickerService folderPickerService, 
        IAppSettingsService appSettingsService,
        IClipboardService clipboardService,
        IFileLauncherService fileLauncherService)
        : this(scanner, folderPickerService, appSettingsService, clipboardService, fileLauncherService, new LocalizationService())
    {
    }

    public MainWindowViewModel(
        IOmsiAssetScanner scanner, 
        IFolderPickerService folderPickerService, 
        IAppSettingsService appSettingsService,
        IClipboardService clipboardService,
        IFileLauncherService fileLauncherService,
        ILocalizationService localizationService,
        IAssetConversionService? conversionService = null)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _folderPickerService = folderPickerService ?? throw new ArgumentNullException(nameof(folderPickerService));
        _appSettingsService = appSettingsService ?? throw new ArgumentNullException(nameof(appSettingsService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _fileLauncherService = fileLauncherService ?? throw new ArgumentNullException(nameof(fileLauncherService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _conversionService = conversionService ?? new AssetConversionService();

        _localizationService.CultureChanged += (s, e) => OnPropertyChanged(string.Empty);
    }

    public async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var lastRoot = await _appSettingsService.GetLastOmsiRootAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(lastRoot))
            {
                RootDirectory = lastRoot;
            }
        }
        catch
        {
            // Do not crash
        }

        try
        {
            var lastLang = await _appSettingsService.GetLastLanguageAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(lastLang))
            {
                _localizationService.SetCulture(lastLang);
            }
        }
        catch
        {
            // Do not crash
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _scanCts?.Cancel();
    }

    [RelayCommand]
    private async Task SelectFolderAsync()
    {
        var folderPath = await _folderPickerService.PickFolderAsync();
        if (!string.IsNullOrEmpty(folderPath))
        {
            await StartScanAsync(folderPath);
        }
    }

    [RelayCommand]
    private void SelectAsset(OmsiAsset? asset)
    {
        SelectedAsset = asset;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    public async Task StartScanAsync(string directoryPath)
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        RootDirectory = directoryPath;
        ErrorMessage = null;
        IsScanning = true;
        IsEmptyState = false;
        _allAssets.Clear();
        Assets.Clear();
        AssetCount = 0;
        FilteredAssetCount = 0;
        HasActiveSearch = !string.IsNullOrWhiteSpace(SearchText);
        SelectedAsset = null;
        HasAssets = false;

        ScanProgressText = _localizationService["ScanningMessage"];

        ScanWarnings.Clear();
        ScanErrors.Clear();
        WarningCount = 0;
        ErrorCount = 0;

        try
        {
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                await _appSettingsService.SaveLastOmsiRootAsync(directoryPath, token);
            }
        }
        catch
        {
            // Non-fatal saving failure
        }

        try
        {
            var progress = new Progress<OmsiScanProgress>(p =>
            {
                ScanProgressText = string.Format(_localizationService["ScanProgressFormat"], p.ParsedAssetCount, System.IO.Path.GetFileName(p.CurrentFilePath));
            });

            var result = await _scanner.ScanAsync(directoryPath, progress, token);

            // Populates discovered assets (including partial results if scan was cancelled)
            foreach (var asset in result.DiscoveredAssets)
            {
                _allAssets.Add(asset);
            }
            AssetCount = _allAssets.Count;
            HasAssets = _allAssets.Count > 0;

            foreach (var warning in result.Warnings)
            {
                ScanWarnings.Add(warning);
            }
            foreach (var error in result.Errors)
            {
                ScanErrors.Add(error);
            }
            WarningCount = ScanWarnings.Count;
            ErrorCount = ScanErrors.Count;

            ApplyFilter();

            if (_allAssets.Count == 0)
            {
                IsEmptyState = true;
            }

            if (token.IsCancellationRequested)
            {
                ScanProgressText = _localizationService["ScanCancelledMessage"];
            }
            else
            {
                ScanProgressText = string.Empty;
            }
        }
        catch (OperationCanceledException)
        {
            // Scanning cancelled
            ScanProgressText = _localizationService["ScanCancelledMessage"];
            ApplyFilter();
            if (_allAssets.Count == 0)
            {
                IsEmptyState = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(_localizationService["ScanFailedMessage"], ex.Message);
            IsEmptyState = _allAssets.Count == 0;
            ScanProgressText = string.Empty;
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void ApplyFilter()
    {
        var tempSelected = SelectedAsset;
        Assets.Clear();
        foreach (var asset in _allAssets)
        {
            if (MatchesFilter(asset))
            {
                Assets.Add(asset);
            }
        }

        UpdateAssetGroups();

        if (tempSelected != null && Assets.Contains(tempSelected))
        {
            SelectedAsset = tempSelected;
        }
        else
        {
            SelectedAsset = null;
        }

        FilteredAssetCount = Assets.Count;
        HasActiveSearch = !string.IsNullOrWhiteSpace(SearchText);
    }

    private void UpdateAssetGroups()
    {
        AssetGroups.Clear();

        var grouped = new Dictionary<string, List<OmsiAsset>>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in Assets)
        {
            string groupName;
            if (GroupingMode == AssetGroupingMode.Folder)
            {
                var normalizedPath = asset.RelativePath.Replace('\\', '/');
                var idx = normalizedPath.IndexOf('/');
                groupName = idx == -1 ? "(Root)" : normalizedPath.Substring(0, idx);
            }
            else
            {
                groupName = asset.Groups != null && asset.Groups.Count > 0 ? asset.Groups[0] : "(Ungrouped)";
            }

            if (!grouped.TryGetValue(groupName, out var list))
            {
                list = new List<OmsiAsset>();
                grouped[groupName] = list;
            }
            list.Add(asset);
        }

        var sortedKeys = grouped.Keys.OrderBy(k => 
        {
            if (k == "(Root)" || k == "(Ungrouped)") return string.Empty;
            return k;
        });

        foreach (var key in sortedKeys)
        {
            var groupVm = new AssetGroupViewModel { Name = key };
            foreach (var asset in grouped[key])
            {
                groupVm.Assets.Add(asset);
            }
            AssetGroups.Add(groupVm);
        }
    }

    private bool MatchesFilter(OmsiAsset asset)
    {
        var filter = SearchText;
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        var tokens = filter.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return true;
        }

        return tokens.All(token =>
        {
            if (asset.DisplayName != null && asset.DisplayName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (asset.RelativePath != null && asset.RelativePath.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (asset.Description != null && asset.Description.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (asset.Groups != null && asset.Groups.Any(g => g != null && g.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (asset.ModelReferences != null && asset.ModelReferences.Any(m => m != null && m.MeshPath != null && m.MeshPath.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        });
    }

    [RelayCommand]
    private async Task CopyAssetPathAsync(CancellationToken cancellationToken)
    {
        if (SelectedAsset == null || string.IsNullOrEmpty(SelectedAsset.SourceScoPath))
        {
            return;
        }

        try
        {
            await _clipboardService.SetTextAsync(SelectedAsset.SourceScoPath, cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(_localizationService["CopyAssetPathFail"], ex.Message);
        }
    }

    [RelayCommand]
    private async Task CopyRelativePathAsync(CancellationToken cancellationToken)
    {
        if (SelectedAsset == null || string.IsNullOrEmpty(SelectedAsset.RelativePath))
        {
            return;
        }

        try
        {
            await _clipboardService.SetTextAsync(SelectedAsset.RelativePath, cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(_localizationService["CopyRelativePathFail"], ex.Message);
        }
    }

    [RelayCommand]
    private async Task OpenAssetFolderAsync(CancellationToken cancellationToken)
    {
        if (SelectedAsset == null || string.IsNullOrEmpty(SelectedAsset.SourceScoPath))
        {
            return;
        }

        try
        {
            var folder = Path.GetDirectoryName(SelectedAsset.SourceScoPath);
            if (!string.IsNullOrEmpty(folder))
            {
                await _fileLauncherService.OpenFolderAsync(folder, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(_localizationService["OpenFolderFail"], ex.Message);
        }
    }

    [RelayCommand]
    private async Task SetCultureAsync(string cultureName)
    {
        _localizationService.SetCulture(cultureName);
        try
        {
            await _appSettingsService.SaveLastLanguageAsync(cultureName);
        }
        catch
        {
            // Non-fatal saving failure
        }
    }

    [RelayCommand]
    private async Task ExportManifestAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedAsset == null)
        {
            return;
        }

        ExportSuccessMessage = null;
        ExportErrorMessage = null;

        string? outputFolder;
        try
        {
            outputFolder = await _folderPickerService.PickFolderAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ExportErrorMessage = string.Format(_localizationService["ExportFolderPickFail"], ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            return;
        }

        try
        {
            var request = new ConversionRequest
            {
                Asset = SelectedAsset,
                TargetOutputDirectory = outputFolder,
                TargetFormat = ConversionTargetFormat.ManifestOnly
            };

            var result = await _conversionService.ConvertAsync(request, cancellationToken);

            if (result.Status == ConversionStatus.Succeeded && result.OutputFiles.Count > 0)
            {
                var writtenPath = result.OutputFiles[0];
                ExportSuccessMessage = string.Format(_localizationService["ExportSuccessFormat"], writtenPath);
            }
            else
            {
                var errorMsg = result.Errors.Count > 0 ? result.Errors[0] : "Unknown error";
                ExportErrorMessage = string.Format(_localizationService["ExportFailFormat"], errorMsg);
            }
        }
        catch (Exception ex)
        {
            ExportErrorMessage = string.Format(_localizationService["ExportFailFormat"], ex.Message);
        }
    }
}

