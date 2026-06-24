using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Scanning;
using OmsiStudio.Core.Services;

namespace OmsiStudio.OmsiFormat.Scanner;

public class OmsiAssetScanner : IOmsiAssetScanner
{
    private readonly IScoFileParser _parser;
    private readonly IOmsiDirectoryScanner _directoryScanner;
    private readonly IOmsiModelReferenceResolver _modelReferenceResolver;

    public OmsiAssetScanner(IScoFileParser parser) 
        : this(parser, new OmsiDirectoryScanner(), new OmsiModelReferenceResolver())
    {
    }

    public OmsiAssetScanner(IScoFileParser parser, IOmsiDirectoryScanner directoryScanner)
        : this(parser, directoryScanner, new OmsiModelReferenceResolver())
    {
    }

    public OmsiAssetScanner(
        IScoFileParser parser, 
        IOmsiDirectoryScanner directoryScanner, 
        IOmsiModelReferenceResolver modelReferenceResolver)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _directoryScanner = directoryScanner ?? throw new ArgumentNullException(nameof(directoryScanner));
        _modelReferenceResolver = modelReferenceResolver ?? throw new ArgumentNullException(nameof(modelReferenceResolver));
    }

    public async IAsyncEnumerable<OmsiAsset> ScanDirectoryAsync(
        string rootDirectory, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            yield break;
        }

        var sceneryObjectsDir = OmsiDirectoryHelper.GetSceneryObjectsDir(rootDirectory);
        if (!Directory.Exists(sceneryObjectsDir))
        {
            yield break;
        }

        await foreach (var file in _directoryScanner.FindScoFilesAsync(rootDirectory, cancellationToken))
        {
            OmsiAsset asset;
            try
            {
                var relativePath = Path.GetRelativePath(sceneryObjectsDir, file);
                asset = _parser.Parse(file, relativePath, out _);
                asset = ResolveModelReferences(rootDirectory, asset, null);
            }
            catch (Exception)
            {
                var relativePath = Path.GetRelativePath(sceneryObjectsDir, file);
                asset = new OmsiAsset
                {
                    DisplayName = Path.GetFileNameWithoutExtension(file),
                    SourceScoPath = file,
                    RelativePath = relativePath,
                    AssetType = OmsiAssetType.SceneryObject
                };
            }

            yield return asset;

            await Task.Yield();
        }
    }

    public Task<OmsiScanResult> ScanAsync(
        string rootDirectory, 
        CancellationToken cancellationToken = default)
    {
        return ScanAsync(rootDirectory, null, cancellationToken);
    }

    public async Task<OmsiScanResult> ScanAsync(
        string rootDirectory, 
        IProgress<OmsiScanProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var discoveredAssets = new List<OmsiAsset>();
        var warnings = new List<string>();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            errors.Add($"OMSI root directory '{rootDirectory}' does not exist.");
            return new OmsiScanResult
            {
                DiscoveredAssets = discoveredAssets,
                Warnings = warnings,
                Errors = errors
            };
        }

        var sceneryObjectsDir = OmsiDirectoryHelper.GetSceneryObjectsDir(rootDirectory);
        if (!Directory.Exists(sceneryObjectsDir))
        {
            errors.Add($"Sceneryobjects directory does not exist under root '{rootDirectory}'.");
            return new OmsiScanResult
            {
                DiscoveredAssets = discoveredAssets,
                Warnings = warnings,
                Errors = errors
            };
        }

        int discoveredCount = 0;
        int parsedCount = 0;

        try
        {
            await foreach (var file in _directoryScanner.FindScoFilesAsync(rootDirectory, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                discoveredCount++;
                OmsiAsset asset;
                var relativePath = Path.GetRelativePath(sceneryObjectsDir, file);

                progress?.Report(new OmsiScanProgress
                {
                    DiscoveredFileCount = discoveredCount,
                    ParsedAssetCount = parsedCount,
                    CurrentFilePath = relativePath
                });

                try
                {
                    asset = _parser.Parse(file, relativePath, out var parserWarnings);
                    if (parserWarnings != null && parserWarnings.Count > 0)
                    {
                        foreach (var warning in parserWarnings)
                        {
                            warnings.Add($"[{Path.GetFileName(file)}] {warning}");
                        }
                    }
                    asset = ResolveModelReferences(rootDirectory, asset, warnings);
                    parsedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Error parsing file '{file}': {ex.Message}");
                    asset = new OmsiAsset
                    {
                        DisplayName = Path.GetFileNameWithoutExtension(file),
                        SourceScoPath = file,
                        RelativePath = relativePath,
                        AssetType = OmsiAssetType.SceneryObject
                    };
                }

                discoveredAssets.Add(asset);
                await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
            // Exit loop gracefully on cancellation and return partial results
        }
        catch (Exception ex)
        {
            errors.Add($"Scan process encountered a fatal error: {ex.Message}");
        }

        return new OmsiScanResult
        {
            DiscoveredAssets = discoveredAssets,
            Warnings = warnings,
            Errors = errors
        };
    }

    private OmsiAsset ResolveModelReferences(
        string rootDirectory, 
        OmsiAsset asset, 
        ICollection<string>? warningsCollector)
    {
        if (asset.ModelReferences == null || asset.ModelReferences.Count == 0)
        {
            return asset;
        }

        var resolvedList = new List<OmsiModelReference>();

        foreach (var modelRef in asset.ModelReferences)
        {
            if (modelRef == null) continue;

            var resolved = _modelReferenceResolver.Resolve(rootDirectory, asset.SourceScoPath, modelRef.MeshPath);
            resolvedList.Add(resolved);

            if (warningsCollector != null)
            {
                if (resolved.ResolutionStatus == OmsiModelReferenceResolutionStatus.Missing)
                {
                    warningsCollector.Add($"[{Path.GetFileName(asset.SourceScoPath)}] Model reference missing: '{modelRef.MeshPath}' (resolved path: '{resolved.ResolvedPath}').");
                }
                else if (resolved.ResolutionStatus == OmsiModelReferenceResolutionStatus.InvalidPath)
                {
                    warningsCollector.Add($"[{Path.GetFileName(asset.SourceScoPath)}] Model reference invalid/traversal: '{modelRef.MeshPath}' (resolved path: '{resolved.ResolvedPath}').");
                }
            }
        }

        return new OmsiAsset
        {
            DisplayName = asset.DisplayName,
            AssetType = asset.AssetType,
            SourceScoPath = asset.SourceScoPath,
            RelativePath = asset.RelativePath,
            Description = asset.Description,
            Groups = asset.Groups,
            ModelReferences = resolvedList,
            TextureReferences = asset.TextureReferences
        };
    }
}
