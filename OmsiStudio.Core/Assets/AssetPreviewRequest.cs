namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents an immutable request to load a 3D asset preview.
/// </summary>
public sealed record AssetPreviewRequest
{
    /// <summary>
    /// Gets the unique identity of the scenery object asset.
    /// </summary>
    public string AssetId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the raw model reference path or resolved file path of the `.o3d` mesh.
    /// </summary>
    public string ModelPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of raw model reference paths or resolved file paths of the `.o3d` meshes.
    /// </summary>
    public IReadOnlyList<string> ModelPaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of resolved model references (including their transformations).
    /// </summary>
    public IReadOnlyList<OmsiModelReference> ModelReferences { get; init; } = Array.Empty<OmsiModelReference>();

    /// <summary>
    /// Gets or sets a value indicating whether model transforms (scale, rotation, translation) should be applied.
    /// </summary>
    public bool ApplyModelTransforms { get; init; } = false;
}
