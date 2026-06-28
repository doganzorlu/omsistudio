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
}
