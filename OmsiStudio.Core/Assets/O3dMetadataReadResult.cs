using System.Collections.Generic;

namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the result of an O3D file metadata inspection pipeline.
/// </summary>
public sealed record O3dMetadataReadResult
{
    /// <summary>
    /// Gets the successfully parsed metadata, if any.
    /// </summary>
    public O3dMetadata? Metadata { get; init; }

    /// <summary>
    /// Gets the read status of the O3D metadata inspection.
    /// </summary>
    public O3dMetadataStatus Status { get; init; } = O3dMetadataStatus.Unknown;

    /// <summary>
    /// Gets the list of structured warnings, errors, or safety diagnostics generated during parsing.
    /// </summary>
    public IReadOnlyList<O3dDiagnostic> Diagnostics { get; init; } = [];
}
