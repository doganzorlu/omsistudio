using System.Collections.Generic;
using System.Threading;

namespace OmsiStudio.Core.Services;

public interface IOmsiDirectoryScanner
{
    IAsyncEnumerable<string> FindScoFilesAsync(
        string omsiRootDirectory,
        CancellationToken cancellationToken = default);
}
