using OmsiStudio.Core.Conversion;

namespace OmsiStudio.Core.Services;

public interface IExportManifestSerializer
{
    string Serialize(ExportManifest manifest);
}
