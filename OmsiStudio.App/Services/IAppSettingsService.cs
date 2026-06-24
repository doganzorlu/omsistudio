using System.Threading;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public interface IAppSettingsService
{
    Task<string?> GetLastOmsiRootAsync(CancellationToken cancellationToken = default);
    Task SaveLastOmsiRootAsync(string rootDirectory, CancellationToken cancellationToken = default);
    Task<string?> GetLastLanguageAsync(CancellationToken cancellationToken = default);
    Task SaveLastLanguageAsync(string cultureName, CancellationToken cancellationToken = default);
}
