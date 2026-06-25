using System.Threading;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public sealed class NullScanCacheService : IScanCacheService
{
    public Task<OmsiScanCacheEntry?> GetAsync(string rootDirectory, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<OmsiScanCacheEntry?>(null);
    }

    public Task SaveAsync(OmsiScanCacheEntry entry, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
