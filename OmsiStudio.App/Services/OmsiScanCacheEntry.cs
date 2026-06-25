using System;
using System.Collections.Generic;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Services;

public sealed class OmsiScanCacheEntry
{
    public string RootDirectory { get; set; } = string.Empty;
    public DateTime CachedAtUtc { get; set; }
    public List<OmsiAsset> Assets { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
