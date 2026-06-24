using OmsiStudio.Core.Conversion;

namespace OmsiStudio.Core.Services;

public interface IExportManifestBuilder
{
    ExportManifest Build(ConversionRequest request, ConversionResult result);
}
