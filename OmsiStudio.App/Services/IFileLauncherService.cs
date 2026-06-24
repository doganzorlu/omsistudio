using System.Threading;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public interface IFileLauncherService
{
    Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default);
}
