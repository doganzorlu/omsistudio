using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Conversion;
using OmsiStudio.Core.Services;

namespace OmsiStudio.Conversion;

public class AssetConversionService : IAssetConversionService
{
    private readonly IExportManifestBuilder _manifestBuilder;
    private readonly IExportManifestWriter _manifestWriter;

    public AssetConversionService() 
        : this(new ExportManifestBuilder(), new ExportManifestWriter(new ExportManifestSerializer()))
    {
    }

    public AssetConversionService(IExportManifestBuilder manifestBuilder, IExportManifestWriter manifestWriter)
    {
        _manifestBuilder = manifestBuilder ?? throw new ArgumentNullException(nameof(manifestBuilder));
        _manifestWriter = manifestWriter ?? throw new ArgumentNullException(nameof(manifestWriter));
    }

    public async Task<ConversionResult> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request == null)
        {
            return new ConversionResult
            {
                Status = ConversionStatus.Failed,
                Errors = new List<string> { "Conversion request cannot be null." }
            };
        }

        if (request.Asset == null || string.IsNullOrEmpty(request.Asset.SourceScoPath))
        {
            return new ConversionResult
            {
                Status = ConversionStatus.Failed,
                Errors = new List<string> { "Source asset path is missing or invalid." }
            };
        }

        if (string.IsNullOrWhiteSpace(request.TargetOutputDirectory))
        {
            return new ConversionResult
            {
                Status = ConversionStatus.Failed,
                Errors = new List<string> { "Target output directory is missing or empty." }
            };
        }

        if (!Path.IsPathFullyQualified(request.TargetOutputDirectory))
        {
            return new ConversionResult
            {
                Status = ConversionStatus.Failed,
                Errors = new List<string> { "Target output directory must be an absolute path." }
            };
        }

        try
        {
            await Task.Yield(); // Avoid blocking

            cancellationToken.ThrowIfCancellationRequested();

            if (request.TargetFormat == ConversionTargetFormat.ManifestOnly)
            {
                var initialResult = new ConversionResult
                {
                    Status = ConversionStatus.Succeeded,
                    Warnings = new List<string> { "This is a placeholder manifest only export." }
                };

                var manifest = _manifestBuilder.Build(request, initialResult);
                var actualManifestFile = await _manifestWriter.WriteAsync(manifest, request.TargetOutputDirectory, cancellationToken);

                return new ConversionResult
                {
                    Status = ConversionStatus.Succeeded,
                    OutputFiles = new List<string> { actualManifestFile },
                    Warnings = initialResult.Warnings
                };
            }

            // Unknown or other unsupported formats (e.g. Gltf, Obj) fail gracefully
            return new ConversionResult
            {
                Status = ConversionStatus.Failed,
                Errors = new List<string> { $"Target format '{request.TargetFormat}' is not supported yet." }
            };
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            return new ConversionResult
            {
                Status = ConversionStatus.Failed,
                Errors = new List<string> { $"An unexpected error occurred: {ex.Message}" }
            };
        }
    }
}
