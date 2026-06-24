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
using OmsiStudio.Core.Services;
using OmsiStudio.OmsiFormat.Parser;
using OmsiStudio.OmsiFormat.Scanner;
using OmsiStudio.App.Services;

namespace OmsiStudio.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IOmsiAssetScanner _scanner;
    private readonly IFolderPickerService _folderPickerService;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private string? _rootDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoSelectionPrompt))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(ShowNoSelectionPrompt))]
    private string? _errorMessage;

    [ObservableProperty]
    private int _assetCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoSelectionPrompt))]
    [NotifyPropertyChangedFor(nameof(HasSelectedAsset))]
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


    private readonly List<OmsiAsset> _allAssets = new();
    public ObservableCollection<OmsiAsset> Assets { get; } = new();

    public MainWindowViewModel()
    {
        var parser = new ScoFileParser();
        _scanner = new OmsiAssetScanner(parser);
        _folderPickerService = new AvaloniaFolderPickerService();
    }

    public MainWindowViewModel(IOmsiAssetScanner scanner) : this(scanner, new AvaloniaFolderPickerService())
    {
    }

    public MainWindowViewModel(IOmsiAssetScanner scanner, IFolderPickerService folderPickerService)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _folderPickerService = folderPickerService ?? throw new ArgumentNullException(nameof(folderPickerService));
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
        SelectedAsset = null;
        HasAssets = false;

        try
        {
            await foreach (var asset in _scanner.ScanDirectoryAsync(directoryPath, token))
            {
                _allAssets.Add(asset);
                AssetCount = _allAssets.Count;
                HasAssets = true;

                if (MatchesFilter(asset))
                {
                    Assets.Add(asset);
                }
            }

            if (_allAssets.Count == 0)
            {
                IsEmptyState = true;
            }
        }
        catch (OperationCanceledException)
        {
            // Scanning cancelled
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Scan failed: {ex.Message}";
            IsEmptyState = _allAssets.Count == 0;
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

        if (tempSelected != null && Assets.Contains(tempSelected))
        {
            SelectedAsset = tempSelected;
        }
        else
        {
            SelectedAsset = null;
        }
    }

    private bool MatchesFilter(OmsiAsset asset)
    {
        var filter = SearchText.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }

        if (asset.RelativePath.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (asset.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (asset.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (asset.Groups.Any(g => g.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}

