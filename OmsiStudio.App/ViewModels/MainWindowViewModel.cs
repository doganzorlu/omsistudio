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
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IScanCacheService _scanCacheService;
    private CancellationTokenSource? _scanCts;
    private readonly object _scanLock = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RootDirectoryDisplay))]
    [NotifyCanExecuteChangedFor(nameof(RefreshScanCommand))]
    private string? _rootDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoSelectionPrompt))]
    [NotifyPropertyChangedFor(nameof(ShowCancellationBanner))]
    [NotifyPropertyChangedFor(nameof(ShowTopScanSummary))]
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
    [NotifyPropertyChangedFor(nameof(HasSelectedAssetTextureReferences))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedAssetTextureReferences))]
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
    public ObservableCollection<AssetTextureReferenceViewModel> SelectedAssetTextureReferences { get; } = new();
    public bool HasSelectedAssetTextureReferences => SelectedAssetTextureReferences.Count > 0;
    public bool HasNoSelectedAssetTextureReferences => !HasSelectedAssetTextureReferences;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScanMessages))]
    [NotifyPropertyChangedFor(nameof(HasScanWarnings))]
    [NotifyPropertyChangedFor(nameof(ScanWarningsDisplay))]
    [NotifyPropertyChangedFor(nameof(ShowTopScanSummary))]
    private int _warningCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScanMessages))]
    [NotifyPropertyChangedFor(nameof(HasScanErrors))]
    [NotifyPropertyChangedFor(nameof(ScanErrorsDisplay))]
    [NotifyPropertyChangedFor(nameof(ShowTopScanSummary))]
    private int _errorCount;

    public bool HasScanMessages => WarningCount > 0 || ErrorCount > 0;
    public bool HasScanWarnings => WarningCount > 0;
    public bool HasScanErrors => ErrorCount > 0;
    public bool ShowTopScanSummary => !IsScanning && HasScanMessages;

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

        SelectedAssetTextureReferences.Clear();
        if (value != null)
        {
            var seen = new Dictionary<string, AssetTextureReferenceViewModel>(StringComparer.OrdinalIgnoreCase);

            if (value.TextureReferences != null)
            {
                var scoSourceLabel = _localizationService["ScoTextureSource"];
                foreach (var tex in value.TextureReferences)
                {
                    if (tex != null && !seen.ContainsKey(tex))
                    {
                        seen.Add(tex, new AssetTextureReferenceViewModel(tex, scoSourceLabel, true));
                    }
                }
            }

            if (value.ModelReferences != null)
            {
                foreach (var modelRef in value.ModelReferences)
                {
                    if (modelRef?.Metadata?.TextureReferences != null)
                    {
                        var meshFileName = Path.GetFileName(modelRef.MeshPath.Replace('\\', '/'));
                        foreach (var o3dTex in modelRef.Metadata.TextureReferences)
                        {
                            if (o3dTex?.Path != null && !seen.ContainsKey(o3dTex.Path))
                            {
                                seen.Add(o3dTex.Path, new AssetTextureReferenceViewModel(o3dTex.Path, meshFileName, false));
                            }
                        }
                    }
                }
            }

            foreach (var vm in seen.Values)
            {
                SelectedAssetTextureReferences.Add(vm);
            }
        }

        OnPropertyChanged(nameof(HasSelectedAssetTextureReferences));
        OnPropertyChanged(nameof(HasNoSelectedAssetTextureReferences));
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
        _uiDispatcher = new InlineUiDispatcher();
        _scanCacheService = new JsonScanCacheService();

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
        IAppSettingsService appSettingsService,
        IScanCacheService? scanCacheService = null)
        : this(scanner, folderPickerService, appSettingsService, new AvaloniaClipboardService(), new ProcessFileLauncherService(), new LocalizationService(), scanCacheService: scanCacheService)
    {
    }

    public MainWindowViewModel(
        IOmsiAssetScanner scanner, 
        IFolderPickerService folderPickerService, 
        IAppSettingsService appSettingsService,
        IClipboardService clipboardService,
        IFileLauncherService fileLauncherService,
        IScanCacheService? scanCacheService = null)
        : this(scanner, folderPickerService, appSettingsService, clipboardService, fileLauncherService, new LocalizationService(), scanCacheService: scanCacheService)
    {
    }

    public MainWindowViewModel(
        IOmsiAssetScanner scanner, 
        IFolderPickerService folderPickerService, 
        IAppSettingsService appSettingsService,
        IClipboardService clipboardService,
        IFileLauncherService fileLauncherService,
        ILocalizationService localizationService,
        IAssetConversionService? conversionService = null,
        IUiDispatcher? uiDispatcher = null,
        IScanCacheService? scanCacheService = null)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _folderPickerService = folderPickerService ?? throw new ArgumentNullException(nameof(folderPickerService));
        _appSettingsService = appSettingsService ?? throw new ArgumentNullException(nameof(appSettingsService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _fileLauncherService = fileLauncherService ?? throw new ArgumentNullException(nameof(fileLauncherService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _conversionService = conversionService ?? new AssetConversionService();
        _uiDispatcher = uiDispatcher ?? new InlineUiDispatcher();
        _scanCacheService = scanCacheService ?? new JsonScanCacheService();

        _localizationService.CultureChanged += (s, e) => OnPropertyChanged(string.Empty);
    }

    public async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
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

        try
        {
            var lastRoot = await _appSettingsService.GetLastOmsiRootAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(lastRoot))
            {
                RootDirectory = lastRoot;

                var cached = await _scanCacheService.GetAsync(lastRoot, cancellationToken);
                if (cached != null)
                {
                    PopulateFromCacheEntry(cached);
                }
                else
                {
                    IsEmptyState = true;
                }
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
            RootDirectory = folderPath;
            try
            {
                await _appSettingsService.SaveLastOmsiRootAsync(folderPath);
            }
            catch
            {
                // Non-fatal
            }

            OmsiScanCacheEntry? cached = null;
            try
            {
                cached = await _scanCacheService.GetAsync(folderPath);
            }
            catch
            {
                // Non-fatal, fallback to null to trigger full scan
            }

            if (cached != null)
            {
                PopulateFromCacheEntry(cached);
                ErrorMessage = null;
                IsScanning = false;
                SelectedAsset = null;
            }
            else
            {
                await StartScanAsync(folderPath);
            }
        }
    }

    public bool CanRefreshScan => !string.IsNullOrWhiteSpace(RootDirectory);

    [RelayCommand(CanExecute = nameof(CanRefreshScan))]
    private async Task RefreshScanAsync()
    {
        if (CanRefreshScan)
        {
            await StartScanAsync(RootDirectory!);
        }
    }

    private void PopulateFromCacheEntry(OmsiScanCacheEntry entry)
    {
        _allAssets.Clear();
        Assets.Clear();
        AssetGroups.Clear();
        ScanWarnings.Clear();
        ScanErrors.Clear();

        HasActiveSearch = !string.IsNullOrWhiteSpace(SearchText);
        SelectedAsset = null;

        _allAssets.AddRange(entry.Assets);
        AssetCount = _allAssets.Count;
        HasAssets = _allAssets.Count > 0;
        IsEmptyState = _allAssets.Count == 0;

        foreach (var asset in _allAssets)
        {
            if (MatchesFilter(asset))
            {
                Assets.Add(asset);
                AddAssetToGroupsIncrementally(asset);
            }
        }
        FilteredAssetCount = Assets.Count;

        foreach (var warning in entry.Warnings)
        {
            ScanWarnings.Add(warning);
        }
        WarningCount = ScanWarnings.Count;

        foreach (var error in entry.Errors)
        {
            ScanErrors.Add(error);
        }
        ErrorCount = ScanErrors.Count;

        UpdateUnsupportedO3dWarningSummary();

        ScanProgressText = string.Format(_localizationService["ScanProgressCompletedFormat"], _allAssets.Count);
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
        _previousUnsupportedSummary = null;
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
            var progress = new SynchronousProgress<OmsiScanProgress>(async p =>
            {
                if (token.IsCancellationRequested) return;

                await RunOnUiAsync(() =>
                {
                    lock (_scanLock)
                    {
                        if (token.IsCancellationRequested) return;

                        if (!string.IsNullOrEmpty(p.CurrentFilePath))
                        {
                            ScanProgressText = string.Format(_localizationService["ScanProgressActiveFormat"], p.ParsedAssetCount);
                        }

                        if (p.NewAsset != null && !_allAssets.Contains(p.NewAsset))
                        {
                            _allAssets.Add(p.NewAsset);
                            AssetCount = _allAssets.Count;
                            HasAssets = _allAssets.Count > 0;
                            IsEmptyState = _allAssets.Count == 0;

                            if (MatchesFilter(p.NewAsset))
                            {
                                Assets.Add(p.NewAsset);
                                FilteredAssetCount = Assets.Count;
                                AddAssetToGroupsIncrementally(p.NewAsset);
                            }
                        }

                        if (p.NewWarnings != null && p.NewWarnings.Count > 0)
                        {
                            foreach (var warning in p.NewWarnings)
                            {
                                if (!ScanWarnings.Contains(warning))
                                {
                                    ScanWarnings.Add(warning);
                                }
                            }
                        }

                        if (p.NewErrors != null && p.NewErrors.Count > 0)
                        {
                            foreach (var error in p.NewErrors)
                            {
                                if (!ScanErrors.Contains(error))
                                {
                                    ScanErrors.Add(error);
                                }
                            }
                            ErrorCount = ScanErrors.Count;
                        }

                        UpdateUnsupportedO3dWarningSummary();
                    }
                });
            });

            var result = await Task.Run(() => _scanner.ScanAsync(directoryPath, progress, token), token);

            OmsiScanCacheEntry? cacheEntry = null;
            await RunOnUiAsync(() =>
            {
                lock (_scanLock)
                {
                    foreach (var asset in result.DiscoveredAssets)
                    {
                        if (!_allAssets.Contains(asset))
                        {
                            _allAssets.Add(asset);
                            if (MatchesFilter(asset))
                            {
                                Assets.Add(asset);
                                AddAssetToGroupsIncrementally(asset);
                            }
                        }
                    }
                    AssetCount = _allAssets.Count;
                    FilteredAssetCount = Assets.Count;
                    HasAssets = _allAssets.Count > 0;

                    foreach (var warning in result.Warnings)
                    {
                        if (!ScanWarnings.Contains(warning))
                        {
                            ScanWarnings.Add(warning);
                        }
                    }

                    foreach (var error in result.Errors)
                    {
                        if (!ScanErrors.Contains(error))
                        {
                            ScanErrors.Add(error);
                        }
                    }
                    ErrorCount = ScanErrors.Count;

                    UpdateUnsupportedO3dWarningSummary();

                    if (token.IsCancellationRequested)
                    {
                        ScanProgressText = _localizationService["ScanProgressCancelled"];
                    }
                    else
                    {
                        ScanProgressText = string.Format(_localizationService["ScanProgressCompletedFormat"], _allAssets.Count);

                        bool hasFatalError = result.Errors.Any(e => e != null && e.Contains("fatal error", StringComparison.OrdinalIgnoreCase));
                        if (!hasFatalError)
                        {
                            cacheEntry = new OmsiScanCacheEntry
                            {
                                RootDirectory = directoryPath,
                                CachedAtUtc = DateTime.UtcNow,
                                Assets = result.DiscoveredAssets.ToList(),
                                Warnings = result.Warnings.ToList(),
                                Errors = result.Errors.ToList()
                            };
                        }
                    }

                    if (_allAssets.Count == 0)
                    {
                        IsEmptyState = true;
                    }
                }
            });

            if (cacheEntry != null)
            {
                try
                {
                    await _scanCacheService.SaveAsync(cacheEntry);
                }
                catch
                {
                    // Non-fatal, do not fail the scan itself if writing cache fails
                }
            }
        }
        catch (OperationCanceledException)
        {
            await RunOnUiAsync(() =>
            {
                ScanProgressText = _localizationService["ScanProgressCancelled"];
                if (_allAssets.Count == 0)
                {
                    IsEmptyState = true;
                }
            });
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                ErrorMessage = string.Format(_localizationService["ScanFailedMessage"], ex.Message);
                IsEmptyState = _allAssets.Count == 0;
                ScanProgressText = string.Empty;
            });
        }
        finally
        {
            await RunOnUiAsync(() => IsScanning = false);
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

    private Task RunOnUiAsync(Action action)
    {
        if (_uiDispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _uiDispatcher.InvokeAsync(action);
    }

    private string? _previousUnsupportedSummary;

    private int CountUnsupportedO3dFiles()
    {
        int count = 0;
        foreach (var asset in _allAssets)
        {
            if (asset.ModelReferences != null)
            {
                foreach (var modelRef in asset.ModelReferences)
                {
                    if (modelRef.IsUnsupportedVersion)
                    {
                        count++;
                    }
                }
            }
        }
        return count;
    }

    private void UpdateUnsupportedO3dWarningSummary()
    {
        int count = CountUnsupportedO3dFiles();
        if (count > 0)
        {
            string summary = string.Format(_localizationService["O3dMetadataLoadFailedSummary"], count);
            if (_previousUnsupportedSummary != null)
            {
                int index = ScanWarnings.IndexOf(_previousUnsupportedSummary);
                if (index >= 0)
                {
                    ScanWarnings[index] = summary;
                }
                else
                {
                    ScanWarnings.Add(summary);
                }
            }
            else
            {
                ScanWarnings.Add(summary);
            }
            _previousUnsupportedSummary = summary;
        }
        else
        {
            if (_previousUnsupportedSummary != null)
            {
                ScanWarnings.Remove(_previousUnsupportedSummary);
                _previousUnsupportedSummary = null;
            }
        }
        WarningCount = ScanWarnings.Count;
    }

    private void AddAssetToGroupsIncrementally(OmsiAsset asset)
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

        var existingGroup = AssetGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        if (existingGroup != null)
        {
            existingGroup.Assets.Add(asset);
        }
        else
        {
            var newGroup = new AssetGroupViewModel { Name = groupName };
            newGroup.Assets.Add(asset);

            int insertIndex = 0;
            for (int i = 0; i < AssetGroups.Count; i++)
            {
                if (CompareGroupNames(groupName, AssetGroups[i].Name) > 0)
                {
                    insertIndex = i + 1;
                }
                else
                {
                    break;
                }
            }
            AssetGroups.Insert(insertIndex, newGroup);
        }
    }

    private int CompareGroupNames(string a, string b)
    {
        string GetSortKey(string s)
        {
            if (s == "(Root)" || s == "(Ungrouped)") return string.Empty;
            return s;
        }
        return string.Compare(GetSortKey(a), GetSortKey(b), StringComparison.OrdinalIgnoreCase);
    }
}
