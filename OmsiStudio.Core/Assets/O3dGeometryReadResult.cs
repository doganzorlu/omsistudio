using System.Collections.Generic;

namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the outcome result of an O3D geometry file reading pipeline.
/// </summary>
public sealed record O3dGeometryReadResult
{
    /// <summary>
    /// Gets the successfully parsed mesh data, if any.
    /// </summary>
    public O3dMeshData? MeshData { get; init; }

    /// <summary>
    /// Gets the read status of the O3D geometry parsing.
    /// </summary>
    public O3dGeometryStatus Status { get; init; } = O3dGeometryStatus.Unknown;

    /// <summary>
    /// Gets the list of structured warnings, errors, or safety diagnostics generated during geometry parsing.
    /// </summary>
    public IReadOnlyList<O3dDiagnostic> Diagnostics { get; init; } = [];
}
