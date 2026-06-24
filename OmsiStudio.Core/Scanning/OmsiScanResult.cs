using System.Collections.Generic;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Scanning;

public sealed class OmsiScanResult
{
    public IReadOnlyList<OmsiAsset> DiscoveredAssets { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}
