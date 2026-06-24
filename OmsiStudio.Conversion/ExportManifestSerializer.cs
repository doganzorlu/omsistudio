using System.Text.Json;
using System.Text.Json.Serialization;
using OmsiStudio.Core.Conversion;
using OmsiStudio.Core.Services;

namespace OmsiStudio.Conversion;

public class ExportManifestSerializer : IExportManifestSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    static ExportManifestSerializer()
    {
        Options.Converters.Add(new JsonStringEnumConverter());
    }

    public string Serialize(ExportManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, Options);
    }
}
