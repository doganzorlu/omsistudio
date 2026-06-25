using System.Threading;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public interface IScanCacheService
{
    Task<OmsiScanCacheEntry?> GetAsync(string rootDirectory, CancellationToken cancellationToken = default);
    Task SaveAsync(OmsiScanCacheEntry entry, CancellationToken cancellationToken = default);
    Task DeleteAsync(string rootDirectory, CancellationToken cancellationToken = default);
}
