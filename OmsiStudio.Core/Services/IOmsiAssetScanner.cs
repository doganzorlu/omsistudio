using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Scanning;

namespace OmsiStudio.Core.Services;

public interface IOmsiAssetScanner
{
    IAsyncEnumerable<OmsiAsset> ScanDirectoryAsync(string rootDirectory, CancellationToken cancellationToken = default);
    Task<OmsiScanResult> ScanAsync(string rootDirectory, CancellationToken cancellationToken = default);
    Task<OmsiScanResult> ScanAsync(string rootDirectory, IProgress<OmsiScanProgress>? progress, CancellationToken cancellationToken = default);
}
