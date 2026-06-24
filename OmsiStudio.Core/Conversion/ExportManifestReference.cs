namespace OmsiStudio.Core.Conversion;

public sealed record ExportManifestReference
{
    public string Path { get; init; } = string.Empty;
    public ExportManifestReferenceKind Kind { get; init; } = ExportManifestReferenceKind.Unknown;
}
