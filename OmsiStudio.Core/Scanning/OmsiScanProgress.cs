namespace OmsiStudio.Core.Scanning;

public class OmsiScanProgress
{
    public int DiscoveredFileCount { get; init; }
    public int ParsedAssetCount { get; init; }
    public string CurrentFilePath { get; init; } = string.Empty;
}
