using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Conversion;

public sealed record ConversionRequest
{
    public OmsiAsset Asset { get; init; } = new();
    public string TargetOutputDirectory { get; init; } = string.Empty;
    public ConversionTargetFormat TargetFormat { get; init; } = ConversionTargetFormat.Unknown;
}
