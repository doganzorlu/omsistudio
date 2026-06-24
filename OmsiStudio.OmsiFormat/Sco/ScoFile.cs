using System.Collections.Generic;

namespace OmsiStudio.OmsiFormat.Sco;

public sealed class ScoFile
{
    public string FilePath { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Groups { get; init; } = [];
    public IReadOnlyList<ScoMeshReference> Meshes { get; init; } = [];
    public IReadOnlyList<string> TextureReferences { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
