using System;
using System.Linq;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Conversion;
using OmsiStudio.Core.Services;

namespace OmsiStudio.Conversion;

public class ExportManifestBuilder : IExportManifestBuilder
{
    private readonly Func<DateTimeOffset> _clock;

    public ExportManifestBuilder(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public ExportManifest Build(ConversionRequest request, ConversionResult result)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var asset = request.Asset ?? new OmsiAsset();

        var meshes = asset.ModelReferences?
            .Where(m => m != null && !string.IsNullOrEmpty(m.MeshPath))
            .Select(m => new ExportManifestReference
            {
                Path = m.MeshPath,
                Kind = ExportManifestReferenceKind.Mesh
            })
            .ToList() ?? [];

        var textures = asset.TextureReferences?
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => new ExportManifestReference
            {
                Path = t,
                Kind = ExportManifestReferenceKind.Texture
            })
            .ToList() ?? [];

        return new ExportManifest
        {
            AssetDisplayName = asset.DisplayName,
            SourceScoPath = asset.SourceScoPath,
            RelativePath = asset.RelativePath,
            TargetFormat = request.TargetFormat,
            GeneratedAtUtc = _clock(),
            Meshes = meshes,
            Textures = textures,
            Warnings = result.Warnings ?? []
        };
    }
}
