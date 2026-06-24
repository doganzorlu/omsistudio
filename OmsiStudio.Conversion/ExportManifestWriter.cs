using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Conversion;
using OmsiStudio.Core.Services;

namespace OmsiStudio.Conversion;

public class ExportManifestWriter : IExportManifestWriter
{
    private readonly IExportManifestSerializer _serializer;

    public ExportManifestWriter(IExportManifestSerializer serializer)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public async Task<string> WriteAsync(ExportManifest manifest, string outputDirectory, CancellationToken cancellationToken = default)
    {
        if (manifest == null)
        {
            throw new ArgumentNullException(nameof(manifest));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be null or empty.", nameof(outputDirectory));
        }

        var filename = GenerateDeterministicFilename(manifest.AssetDisplayName, manifest.SourceScoPath);
        var fullPath = Path.Combine(outputDirectory, filename);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var jsonString = _serializer.Serialize(manifest);
        await File.WriteAllTextAsync(fullPath, jsonString, cancellationToken);

        return fullPath;
    }

    public static string GenerateDeterministicFilename(string? assetDisplayName, string? scoFilePath)
    {
        string? baseName = null;

        if (!string.IsNullOrWhiteSpace(scoFilePath))
        {
            try
            {
                baseName = Path.GetFileNameWithoutExtension(scoFilePath);
            }
            catch
            {
                // Ignore path errors
            }
        }

        if (string.IsNullOrWhiteSpace(baseName) && !string.IsNullOrWhiteSpace(assetDisplayName))
        {
            baseName = assetDisplayName;
        }

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "asset";
        }

        var invalidChars = new System.Collections.Generic.HashSet<char>(Path.GetInvalidFileNameChars())
        {
            '\\', '/', ':', '*', '?', '"', '<', '>', '|'
        };
        var sanitized = new string(baseName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "asset";
        }

        return $"{sanitized}_manifest.json";
    }
}
