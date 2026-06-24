using System.Collections.Generic;

namespace OmsiStudio.Core.Assets;

public sealed class OmsiAsset
{
    public string DisplayName { get; init; } = string.Empty;
    public OmsiAssetType AssetType { get; init; } = OmsiAssetType.Unknown;
    public string SourceScoPath { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Groups { get; init; } = [];
    public IReadOnlyList<OmsiModelReference> ModelReferences { get; init; } = [];

    // Helper properties for UI data binding
    public bool HasGroups => Groups != null && Groups.Count > 0;
    public bool HasNoGroups => !HasGroups;
    public bool HasMeshes => ModelReferences != null && ModelReferences.Count > 0;
    public bool HasNoMeshes => !HasMeshes;
}
