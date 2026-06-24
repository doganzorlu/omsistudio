using System.Collections.ObjectModel;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.ViewModels;

public sealed class AssetGroupViewModel
{
    public string Name { get; init; } = string.Empty;
    public ObservableCollection<OmsiAsset> Assets { get; } = new();
}
