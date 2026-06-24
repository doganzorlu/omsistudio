using System;
using System.Collections.Generic;

namespace OmsiStudio.Core.Conversion;

public sealed record ExportManifest
{
    public string AssetDisplayName { get; init; } = string.Empty;
    public string SourceScoPath { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public ConversionTargetFormat TargetFormat { get; init; } = ConversionTargetFormat.Unknown;
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public IReadOnlyList<ExportManifestReference> Meshes { get; init; } = [];
    public IReadOnlyList<ExportManifestReference> Textures { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
