using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.OmsiFormat.Scanner;

public class OmsiAssetScanner : IOmsiAssetScanner
{
    private readonly IScoFileParser _parser;
    private readonly IOmsiDirectoryScanner _directoryScanner;

    public OmsiAssetScanner(IScoFileParser parser) : this(parser, new OmsiDirectoryScanner())
    {
    }

    public OmsiAssetScanner(IScoFileParser parser, IOmsiDirectoryScanner directoryScanner)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _directoryScanner = directoryScanner ?? throw new ArgumentNullException(nameof(directoryScanner));
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

        await foreach (var file in _directoryScanner.FindScoFilesAsync(rootDirectory, cancellationToken))
        {
            OmsiAsset asset;
            try
            {
                var relativePath = Path.GetRelativePath(sceneryObjectsDir, file);
                asset = _parser.Parse(file, relativePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning file {file}: {ex.Message}");
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
}
