using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Conversion;

namespace OmsiStudio.Core.Services;

public interface IExportManifestWriter
{
    Task<string> WriteAsync(ExportManifest manifest, string outputDirectory, CancellationToken cancellationToken = default);
}
