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
using OmsiStudio.App.Services.Rendering;
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
    private readonly IAssetPreviewLoader _previewLoader;
    private CancellationTokenSource? _previewCts;
    private readonly object _previewLock = new();
    private readonly IMaterialTextureBindingService? _materialTextureBindingService;
    [ObservableProperty]
    private IRendererHost _rendererHost;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RootDirectoryDisplay))]
    [NotifyCanExecuteChangedFor(nameof(RefreshScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCacheAndRefreshCommand))]
    private string? _rootDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoSelectionPrompt))]
    [NotifyPropertyChangedFor(nameof(ShowCancellationBanner))]
    [NotifyPropertyChangedFor(nameof(ShowTopScanSummary))]
    [NotifyCanExecuteChangedFor(nameof(RefreshScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCacheAndRefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectFolderCommand))]
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
    private IReadOnlyList<MaterialTextureBinding>? _previewTextureBindings;

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
        SelectedPreviewModelReference = null;
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

        OmsiModelReference? firstResolved = null;
        if (value?.ModelReferences != null)
        {
            foreach (var modelRef in value.ModelReferences)
            {
                if (modelRef != null && modelRef.ResolutionStatus == OmsiModelReferenceResolutionStatus.Resolved)
                {
                    firstResolved = modelRef;
                    break;
                }
            }
        }

        if (firstResolved == null)
        {
            lock (_previewLock)
            {
                _previewCts?.Cancel();
                _previewCts = null;
            }
            PreviewStatus = AssetPreviewStatus.Idle;
            PreviewResult = null;
            PreviewTextureBindings = null;
            RendererHost?.SetMesh(null);
        }

        SelectedPreviewModelReference = firstResolved;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewLoading))]
    [NotifyPropertyChangedFor(nameof(PreviewStatusText))]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    private AssetPreviewStatus _previewStatus = AssetPreviewStatus.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewDiagnostics))]
    [NotifyPropertyChangedFor(nameof(HasPreviewDiagnostics))]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    [NotifyPropertyChangedFor(nameof(PreviewMeshSummaryText))]
    [NotifyPropertyChangedFor(nameof(HasPreviewBounds))]
    [NotifyPropertyChangedFor(nameof(PreviewBoundsSummary))]
    [NotifyPropertyChangedFor(nameof(PreviewDiagnosticsBorderBrush))]
    [NotifyPropertyChangedFor(nameof(PreviewDiagnosticsBackground))]
    [NotifyPropertyChangedFor(nameof(PreviewDiagnosticsHeaderColor))]
    [NotifyPropertyChangedFor(nameof(PreviewDiagnosticsTextColor))]
    private AssetPreviewResult? _previewResult;

    public ObservableCollection<MaterialDisplayItem> PreviewMaterials { get; } = [];

    [ObservableProperty]
    private int _renderVersion;

    private bool _debugTriangleEnabled;
    public bool DebugTriangleEnabled
    {
        get => _debugTriangleEnabled;
        set
        {
            if (SetProperty(ref _debugTriangleEnabled, value))
            {
                if (RendererHost != null)
                {
                    RendererHost.DebugTriangleEnabled = value;
                }
                RenderVersion++;
                OnPropertyChanged(nameof(HasPreview));
            }
        }
    }

    public bool LastFrameDrawAttempted => RendererHost?.LastFrameDrawAttempted ?? false;
    public int LastFrameUploadedVertexCount => RendererHost?.LastFrameUploadedVertexCount ?? 0;
    public int LastFrameUploadedIndexCount => RendererHost?.LastFrameUploadedIndexCount ?? 0;
    public string LastGlError => RendererHost?.LastGlError ?? "NoError";

    [ObservableProperty]
    private bool _isOpenGlInitialized;

    [ObservableProperty]
    private bool _isRendererHostInitialized;

    [ObservableProperty]
    private int _openGlRenderCallCount;

    [ObservableProperty]
    private string? _lastViewportError;

    public void NotifyRendererDebugStateChanged()
    {
        OnPropertyChanged(nameof(LastFrameDrawAttempted));
        OnPropertyChanged(nameof(LastFrameUploadedVertexCount));
        OnPropertyChanged(nameof(LastFrameUploadedIndexCount));
        OnPropertyChanged(nameof(LastGlError));
    }

    public string PreviewDiagnosticsBorderBrush =>
        PreviewDiagnostics.Any(d => d.Severity == O3dDiagnosticSeverity.Error)
            ? "#ef4444"
            : "#f59e0b";

    public string PreviewDiagnosticsBackground =>
        PreviewDiagnostics.Any(d => d.Severity == O3dDiagnosticSeverity.Error)
            ? "#2d1c1c"
            : "#2b2214";

    public string PreviewDiagnosticsHeaderColor =>
        PreviewDiagnostics.Any(d => d.Severity == O3dDiagnosticSeverity.Error)
            ? "#f87171"
            : "#fbbf24";

    public string PreviewDiagnosticsTextColor =>
        PreviewDiagnostics.Any(d => d.Severity == O3dDiagnosticSeverity.Error)
            ? "#fca5a5"
            : "#fde68a";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPreviewModelReference))]
    [NotifyPropertyChangedFor(nameof(PreviewModelDisplayName))]
    private OmsiModelReference? _selectedPreviewModelReference;

    public bool HasSelectedPreviewModelReference => SelectedPreviewModelReference != null;

    public IReadOnlyList<O3dDiagnostic> PreviewDiagnostics => PreviewResult?.Diagnostics ?? [];

    public bool HasPreview => (PreviewStatus == AssetPreviewStatus.Success && PreviewResult?.MeshData != null) || DebugTriangleEnabled;

    public bool HasPreviewDiagnostics => PreviewDiagnostics.Count > 0;

    public bool IsPreviewLoading => PreviewStatus == AssetPreviewStatus.Loading;

    public string PreviewStatusText => _localizationService["PreviewStatus_" + PreviewStatus.ToString()];

    public string PreviewModelDisplayName => SelectedPreviewModelReference?.MeshPath ?? string.Empty;

    [ObservableProperty]
    private float _cameraYaw = 45f;

    [ObservableProperty]
    private float _cameraPitch = -30f;

    [ObservableProperty]
    private float _cameraDistance = 5f;

    [ObservableProperty]
    private bool _showBoundingBox;

    [ObservableProperty]
    private bool _enableExperimentalGlPreview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisualModeInt))]
    private SoftwareViewportVisualMode _visualMode = SoftwareViewportVisualMode.Solid;

    public int VisualModeInt
    {
        get => (int)VisualMode;
        set
        {
            if (value >= 0 && value <= 2)
            {
                VisualMode = (SoftwareViewportVisualMode)value;
            }
        }
    }

    partial void OnCameraYawChanged(float value) => UpdateRendererHostCamera();

    partial void OnCameraPitchChanged(float value)
    {
        var clamped = Math.Clamp(value, -89f, 89f);
        if (Math.Abs(clamped - value) > 1e-5f)
        {
            CameraPitch = clamped;
            return;
        }
        UpdateRendererHostCamera();
    }

    partial void OnCameraDistanceChanged(float value)
    {
        var clamped = Math.Clamp(value, 0.5f, 50f);
        if (Math.Abs(clamped - value) > 1e-5f)
        {
            CameraDistance = clamped;
            return;
        }
        UpdateRendererHostCamera();
    }

    partial void OnRendererHostChanged(IRendererHost value)
    {
        if (value != null)
        {
            value.DebugTriangleEnabled = DebugTriangleEnabled;
        }
        UpdateRendererHostCamera();
    }

    private void UpdateRendererHostCamera()
    {
        RendererHost?.SetCamera(new PreviewCameraState
        {
            Yaw = CameraYaw,
            Pitch = CameraPitch,
            Distance = CameraDistance
        });
    }

    [RelayCommand]
    private void ResetCamera()
    {
        CameraYaw = 45f;
        CameraPitch = -30f;
        CameraDistance = 5f;
        UpdateRendererHostCamera();
    }

    [RelayCommand]
    private void OrbitYawLeft()
    {
        CameraYaw = (CameraYaw - 15f) % 360f;
    }

    [RelayCommand]
    private void OrbitYawRight()
    {
        CameraYaw = (CameraYaw + 15f) % 360f;
    }

    [RelayCommand]
    private void OrbitPitchUp()
    {
        CameraPitch = Math.Clamp(CameraPitch + 15f, -89f, 89f);
    }

    [RelayCommand]
    private void OrbitPitchDown()
    {
        CameraPitch = Math.Clamp(CameraPitch - 15f, -89f, 89f);
    }

    [RelayCommand]
    private void ZoomIn()
    {
        CameraDistance = Math.Clamp(CameraDistance - 1f, 0.5f, 50f);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        CameraDistance = Math.Clamp(CameraDistance + 1f, 0.5f, 50f);
    }

    public bool HasPreviewBounds => PreviewResult?.Bounds != null;

    public string PreviewBoundsSummary
    {
        get
        {
            var bounds = PreviewResult?.Bounds;
            if (bounds == null)
            {
                return string.Empty;
            }
            return string.Format(_localizationService["PreviewBounds"], $"Min ({bounds.Min.X:F3}, {bounds.Min.Y:F3}, {bounds.Min.Z:F3}) Max ({bounds.Max.X:F3}, {bounds.Max.Y:F3}, {bounds.Max.Z:F3}) Size ({bounds.Size.X:F3}, {bounds.Size.Y:F3}, {bounds.Size.Z:F3})");
        }
    }

    public string PreviewMeshSummaryText
    {
        get
        {
            var meshData = PreviewResult?.MeshData;
            if (meshData == null)
            {
                return string.Empty;
            }
            return string.Format(_localizationService["PreviewMeshSummary"], meshData.Vertices.Count, meshData.Triangles.Count, meshData.MaterialSlots.Count);
        }
    }

    partial void OnSelectedPreviewModelReferenceChanged(OmsiModelReference? value)
    {
        if (value != null)
        {
            _ = LoadPreviewCommand.ExecuteAsync(null);
        }
        else
        {
            lock (_previewLock)
            {
                _previewCts?.Cancel();
                _previewCts = null;
            }
            PreviewStatus = AssetPreviewStatus.Idle;
            PreviewResult = null;
            PreviewTextureBindings = null;
            RendererHost?.SetMesh(null);
        }
    }

    [RelayCommand]
    private void CancelPreview()
    {
        lock (_previewLock)
        {
            _previewCts?.Cancel();
        }
        PreviewTextureBindings = null;
    }

    public static string GetSceneryObjectsRoot(string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return string.Empty;
        }

        string trimmed = rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string lastSegment = Path.GetFileName(trimmed);
        if (string.Equals(lastSegment, "Sceneryobjects", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return Path.Combine(trimmed, "Sceneryobjects");
    }

    [RelayCommand]
    private async Task LoadPreviewAsync(OmsiModelReference? modelReference = null)
    {
        if (modelReference != null)
        {
            if (SelectedPreviewModelReference != modelReference)
            {
                SelectedPreviewModelReference = modelReference;
                return;
            }
        }

        var modelRef = SelectedPreviewModelReference;
        if (modelRef == null)
        {
            return;
        }

        CancellationToken token;
        lock (_previewLock)
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            token = _previewCts.Token;
        }

        PreviewStatus = AssetPreviewStatus.Loading;
        PreviewResult = null;
        PreviewTextureBindings = null;

        try
        {
            var request = new AssetPreviewRequest
            {
                AssetId = SelectedAsset?.RelativePath ?? string.Empty,
                ModelPath = modelRef.ResolvedPath
            };

            var result = await _previewLoader.LoadAsync(request, token);

            token.ThrowIfCancellationRequested();

            IReadOnlyList<MaterialTextureBinding>? bindings = null;
            if (result.Status == AssetPreviewStatus.Success && result.MeshData != null && _materialTextureBindingService != null)
            {
                try
                {
                    bindings = await _materialTextureBindingService.BindAsync(
                        result.MeshData,
                        modelRef.ResolvedPath,
                        GetSceneryObjectsRoot(RootDirectory),
                        token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Ignore texture binding errors so they don't fail the overall preview
                }
            }

            token.ThrowIfCancellationRequested();

            await RunOnUiAsync(() =>
            {
                lock (_previewLock)
                {
                    if (token == _previewCts?.Token && !token.IsCancellationRequested)
                    {
                        PreviewResult = result;
                        PreviewStatus = result.Status;
                        PreviewTextureBindings = bindings;
                        if (result.Status == AssetPreviewStatus.Success && result.MeshData != null)
                        {
                            RendererHost?.SetMesh(result.MeshData);
                            RenderVersion++;
                        }
                        else
                        {
                            RendererHost?.SetMesh(null);
                        }
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            await RunOnUiAsync(() =>
            {
                lock (_previewLock)
                {
                    if (token == _previewCts?.Token)
                    {
                        PreviewStatus = AssetPreviewStatus.Cancelled;
                        PreviewTextureBindings = null;
                        RendererHost?.SetMesh(null);
                    }
                }
            });
        }
        catch (Exception)
        {
            await RunOnUiAsync(() =>
            {
                lock (_previewLock)
                {
                    if (token == _previewCts?.Token)
                    {
                        PreviewStatus = AssetPreviewStatus.Failed;
                        PreviewTextureBindings = null;
                        RendererHost?.SetMesh(null);
                    }
                }
            });
        }
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
        _previewLoader = new NullAssetPreviewLoader();
        _rendererHost = new OpenGlRendererHost();

        _localizationService.CultureChanged += (s, e) =>
        {
            UpdateScanProgressText();
            UpdatePreviewMaterials();
            OnPropertyChanged(string.Empty);
        };
        UpdateRendererHostCamera();
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
        IScanCacheService? scanCacheService = null,
        IAssetPreviewLoader? previewLoader = null,
        IRendererHost? rendererHost = null,
        IMaterialTextureBindingService? materialTextureBindingService = null)
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
        _previewLoader = previewLoader ?? new NullAssetPreviewLoader();
        _rendererHost = rendererHost ?? new OpenGlRendererHost();
        _materialTextureBindingService = materialTextureBindingService;

        _localizationService.CultureChanged += (s, e) =>
        {
            UpdateScanProgressText();
            UpdatePreviewMaterials();
            OnPropertyChanged(string.Empty);
        };
        UpdateRendererHostCamera();
    }

    partial void OnPreviewResultChanged(AssetPreviewResult? value)
    {
        UpdatePreviewMaterials();
    }

    partial void OnPreviewTextureBindingsChanged(IReadOnlyList<MaterialTextureBinding>? value)
    {
        UpdatePreviewMaterials();
    }

    public bool HasPreviewMaterials => PreviewMaterials.Count > 0;

    private void UpdatePreviewMaterials()
    {
        PreviewMaterials.Clear();
        var mesh = PreviewResult?.MeshData;
        if (mesh?.MaterialSlots != null)
        {
            for (int i = 0; i < mesh.MaterialSlots.Count; i++)
            {
                var slot = mesh.MaterialSlots[i];
                string materialName = string.IsNullOrEmpty(slot.MaterialName) ? $"Material {i}" : slot.MaterialName;
                string textureRef = string.IsNullOrEmpty(slot.TextureReference) 
                    ? _localizationService["MaterialTextureMissing"] 
                    : slot.TextureReference;

                var color = MaterialColorSelector.GetMaterialColor(mesh, i);

                MaterialTextureBinding? binding = null;
                if (PreviewTextureBindings != null)
                {
                    foreach (var b in PreviewTextureBindings)
                    {
                        if (b.MaterialIndex == i)
                        {
                            binding = b;
                            break;
                        }
                    }
                }

                string notBoundText = _localizationService["MaterialNotBound"];
                PreviewMaterials.Add(new MaterialDisplayItem(materialName, textureRef, color, notBoundText, binding));
            }
        }
        OnPropertyChanged(nameof(HasPreviewMaterials));
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

    public bool CanSelectFolder => !IsScanning;

    [RelayCommand(CanExecute = nameof(CanSelectFolder))]
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

    public bool CanRefreshScan => !string.IsNullOrWhiteSpace(RootDirectory) && !IsScanning;

    [RelayCommand(CanExecute = nameof(CanRefreshScan))]
    private async Task RefreshScanAsync()
    {
        if (CanRefreshScan)
        {
            await StartScanAsync(RootDirectory!);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefreshScan))]
    private async Task ClearCacheAndRefreshAsync()
    {
        if (CanRefreshScan)
        {
            try
            {
                await _scanCacheService.DeleteAsync(RootDirectory!);
            }
            catch
            {
                // Non-fatal
            }
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

        _currentScanStatusState = ScanStatusState.LoadedFromCache;
        _statusAssetCount = _allAssets.Count;
        _statusCacheTime = entry.CachedAtUtc;
        UpdateScanProgressText();
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

        _currentScanStatusState = ScanStatusState.Scanning;
        _statusAssetCount = 0;
        UpdateScanProgressText();

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
                            _currentScanStatusState = ScanStatusState.Scanning;
                            _statusAssetCount = p.ParsedAssetCount;
                            UpdateScanProgressText();
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
                        _currentScanStatusState = ScanStatusState.Cancelled;
                        UpdateScanProgressText();
                    }
                    else
                    {
                        _currentScanStatusState = ScanStatusState.Completed;
                        _statusAssetCount = _allAssets.Count;
                        UpdateScanProgressText();

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
                _currentScanStatusState = ScanStatusState.Cancelled;
                UpdateScanProgressText();
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
                _currentScanStatusState = ScanStatusState.Idle;
                UpdateScanProgressText();
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

    private enum ScanStatusState
    {
        Idle,
        Scanning,
        Cancelled,
        Completed,
        LoadedFromCache
    }

    private ScanStatusState _currentScanStatusState = ScanStatusState.Idle;
    private int _statusAssetCount;
    private DateTime _statusCacheTime;

    private void UpdateScanProgressText()
    {
        switch (_currentScanStatusState)
        {
            case ScanStatusState.Scanning:
                ScanProgressText = string.Format(_localizationService["ScanProgressActiveFormat"], _statusAssetCount);
                break;
            case ScanStatusState.Cancelled:
                ScanProgressText = _localizationService["ScanProgressCancelled"];
                break;
            case ScanStatusState.Completed:
                ScanProgressText = string.Format(_localizationService["ScanProgressCompletedFormat"], _statusAssetCount);
                break;
            case ScanStatusState.LoadedFromCache:
                if (_statusCacheTime != DateTime.MinValue && _statusCacheTime != default)
                {
                    var timeStr = _statusCacheTime.ToLocalTime().ToString("g");
                    ScanProgressText = string.Format(_localizationService["LoadedFromCacheFormat"], _statusAssetCount, timeStr);
                }
                else
                {
                    ScanProgressText = string.Format(_localizationService["LoadedFromCacheShortFormat"], _statusAssetCount);
                }
                break;
            default:
                if (IsScanning)
                {
                    ScanProgressText = _localizationService["ScanningMessage"];
                }
                else
                {
                    ScanProgressText = string.Empty;
                }
                break;
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
