using System.Collections.Generic;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Scanning;

public class OmsiScanProgress
{
    public int DiscoveredFileCount { get; init; }
    public int ParsedAssetCount { get; init; }
    public string CurrentFilePath { get; init; } = string.Empty;
    public OmsiAsset? NewAsset { get; init; }
    public IReadOnlyList<string>? NewWarnings { get; init; }
    public IReadOnlyList<string>? NewErrors { get; init; }
}

