using System.Collections.Generic;

namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the output result of an asset preview load operation.
/// </summary>
public sealed record AssetPreviewResult
{
    /// <summary>
    /// Gets the status of the preview load operation.
    /// </summary>
    public AssetPreviewStatus Status { get; init; } = AssetPreviewStatus.Unknown;

    /// <summary>
    /// Gets the loaded mesh data, if successful.
    /// </summary>
    public O3dMeshData? MeshData { get; init; }

    /// <summary>
    /// Gets the computed bounds of the mesh, if successful.
    /// </summary>
    public MeshBounds? Bounds { get; init; }

    /// <summary>
    /// Gets the diagnostics associated with the preview load.
    /// </summary>
    public IReadOnlyList<O3dDiagnostic> Diagnostics { get; init; } = [];
}
