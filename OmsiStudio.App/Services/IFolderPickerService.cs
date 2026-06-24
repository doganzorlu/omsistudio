using System.Threading;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}
