using System;
using System.Collections.Generic;

namespace OmsiStudio.Core.Assets;

public sealed class OmsiModelReference
{
    public string MeshPath { get; init; } = string.Empty;
    public string ResolvedPath { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public OmsiModelReferenceResolutionStatus ResolutionStatus { get; init; } = OmsiModelReferenceResolutionStatus.Unknown;

    /// <summary>
    /// Gets the optional parsed O3D metadata.
    /// </summary>
    public O3dMetadata? Metadata { get; init; }

    /// <summary>
    /// Gets the status of the O3D metadata reading.
    /// </summary>
    public O3dMetadataStatus MetadataStatus { get; init; } = O3dMetadataStatus.Unknown;

    /// <summary>
    /// Gets the cumulative diagnostics generated while reading O3D metadata.
    /// </summary>
    public IReadOnlyList<O3dDiagnostic> MetadataDiagnostics { get; init; } = Array.Empty<O3dDiagnostic>();

    /// <summary>
    /// Gets a value indicating whether metadata is present.
    /// </summary>
    public bool HasMetadata => Metadata != null;

    /// <summary>
    /// Gets a value indicating whether metadata is absent.
    /// </summary>
    public bool HasNoMetadata => Metadata == null;

    /// <summary>
    /// Gets a value indicating whether there are metadata diagnostics.
    /// </summary>
    public bool HasMetadataDiagnostics => MetadataDiagnostics.Count > 0 && MetadataDiagnostics.Any(d => d.Severity == O3dDiagnosticSeverity.Error);

    /// <summary>
    /// Gets a value indicating whether the model format version is unsupported.
    /// </summary>
    public bool IsUnsupportedVersion => MetadataStatus == O3dMetadataStatus.Unsupported;

    public OmsiModelReference()
    {
    }

    public OmsiModelReference(string meshPath)
    {
        MeshPath = meshPath;
    }

    public OmsiModelReference(string meshPath, string resolvedPath, bool exists, OmsiModelReferenceResolutionStatus resolutionStatus)
    {
        MeshPath = meshPath;
        ResolvedPath = resolvedPath;
        Exists = exists;
        ResolutionStatus = resolutionStatus;
    }
}

