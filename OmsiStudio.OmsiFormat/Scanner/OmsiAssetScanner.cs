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
using OmsiStudio.OmsiFormat.Parser;

namespace OmsiStudio.OmsiFormat.Scanner;

public class OmsiAssetScanner : IOmsiAssetScanner
{
    private readonly IScoFileParser _parser;
    private readonly IOmsiDirectoryScanner _directoryScanner;
    private readonly IOmsiModelReferenceResolver _modelReferenceResolver;
    private readonly IO3dMetadataReader _metadataReader;

    public OmsiAssetScanner(IScoFileParser parser) 
        : this(parser, new OmsiDirectoryScanner(), new OmsiModelReferenceResolver(), new O3dMetadataReader())
    {
    }

    public OmsiAssetScanner(IScoFileParser parser, IOmsiDirectoryScanner directoryScanner)
        : this(parser, directoryScanner, new OmsiModelReferenceResolver(), new O3dMetadataReader())
    {
    }

    public OmsiAssetScanner(
        IScoFileParser parser, 
        IOmsiDirectoryScanner directoryScanner, 
        IOmsiModelReferenceResolver modelReferenceResolver)
        : this(parser, directoryScanner, modelReferenceResolver, new O3dMetadataReader())
    {
    }

    public OmsiAssetScanner(
        IScoFileParser parser, 
        IOmsiDirectoryScanner directoryScanner, 
        IOmsiModelReferenceResolver modelReferenceResolver,
        IO3dMetadataReader metadataReader)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _directoryScanner = directoryScanner ?? throw new ArgumentNullException(nameof(directoryScanner));
        _modelReferenceResolver = modelReferenceResolver ?? throw new ArgumentNullException(nameof(modelReferenceResolver));
        _metadataReader = metadataReader ?? throw new ArgumentNullException(nameof(metadataReader));
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
                asset = await ResolveModelReferencesAndMetadataAsync(rootDirectory, asset, null, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
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
                    asset = await ResolveModelReferencesAndMetadataAsync(rootDirectory, asset, warnings, cancellationToken);
                    parsedCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
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

    private async Task<OmsiAsset> ResolveModelReferencesAndMetadataAsync(
        string rootDirectory, 
        OmsiAsset asset, 
        ICollection<string>? warningsCollector,
        CancellationToken cancellationToken)
    {
        if (asset.ModelReferences == null || asset.ModelReferences.Count == 0)
        {
            return asset;
        }

        var resolvedList = new List<OmsiModelReference>();

        foreach (var modelRef in asset.ModelReferences)
        {
            if (modelRef == null) continue;

            cancellationToken.ThrowIfCancellationRequested();

            var resolved = _modelReferenceResolver.Resolve(rootDirectory, asset.SourceScoPath, modelRef.MeshPath);

            var shouldReadMetadata = resolved.Exists &&
                                     resolved.ResolutionStatus == OmsiModelReferenceResolutionStatus.Resolved &&
                                     !string.IsNullOrEmpty(resolved.ResolvedPath) &&
                                     Path.GetExtension(resolved.ResolvedPath).Equals(".o3d", StringComparison.OrdinalIgnoreCase);

            if (shouldReadMetadata)
            {
                try
                {
                    var readResult = await _metadataReader.ReadAsync(resolved.ResolvedPath, cancellationToken);
                    resolved = new OmsiModelReference(resolved.MeshPath, resolved.ResolvedPath, resolved.Exists, resolved.ResolutionStatus)
                    {
                        Metadata = readResult.Metadata,
                        MetadataStatus = readResult.Status,
                        MetadataDiagnostics = readResult.Diagnostics
                    };

                    if (warningsCollector != null && readResult.Diagnostics != null)
                    {
                        foreach (var diag in readResult.Diagnostics)
                        {
                            warningsCollector.Add($"[{Path.GetFileName(asset.SourceScoPath)}] Model reference metadata warning/error in '{modelRef.MeshPath}': [{diag.Code}] {diag.Message}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    resolved = new OmsiModelReference(resolved.MeshPath, resolved.ResolvedPath, resolved.Exists, resolved.ResolutionStatus)
                    {
                        MetadataStatus = O3dMetadataStatus.Failed
                    };
                    warningsCollector?.Add($"[{Path.GetFileName(asset.SourceScoPath)}] Failed to read metadata for '{modelRef.MeshPath}': {ex.Message}");
                }
            }

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
