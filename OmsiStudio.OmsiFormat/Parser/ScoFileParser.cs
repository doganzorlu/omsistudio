using System;
using System.IO;
using System.Linq;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;
using OmsiStudio.OmsiFormat.Sco;

namespace OmsiStudio.OmsiFormat.Parser;

public class ScoFileParser : IScoFileParser
{
    private readonly ScoParser _parser = new();

    public OmsiAsset Parse(string filePath, string relativePath)
    {
        var scoFile = _parser.ParseFile(filePath);

        return new OmsiAsset
        {
            DisplayName = !string.IsNullOrEmpty(scoFile.FriendlyName)
                ? scoFile.FriendlyName
                : (string.IsNullOrEmpty(filePath) ? string.Empty : Path.GetFileNameWithoutExtension(filePath)),
            AssetType = OmsiAssetType.SceneryObject,
            SourceScoPath = filePath,
            RelativePath = relativePath,
            Description = scoFile.Description,
            Groups = scoFile.Groups,
            ModelReferences = scoFile.Meshes.Select(m => new OmsiModelReference(m.MeshPath)).ToList()
        };
    }
}
