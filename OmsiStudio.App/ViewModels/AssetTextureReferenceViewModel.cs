using System;

namespace OmsiStudio.App.ViewModels;

public sealed class AssetTextureReferenceViewModel
{
    public string Path { get; }
    public string Source { get; }
    public bool IsScoSource { get; }

    public AssetTextureReferenceViewModel(string path, string source, bool isScoSource)
    {
        Path = path ?? string.Empty;
        Source = source ?? string.Empty;
        IsScoSource = isScoSource;
    }
}
