using System.Collections.Generic;
using System.Threading;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

public interface IOmsiAssetScanner
{
    IAsyncEnumerable<OmsiAsset> ScanDirectoryAsync(string rootDirectory, CancellationToken cancellationToken = default);
}
