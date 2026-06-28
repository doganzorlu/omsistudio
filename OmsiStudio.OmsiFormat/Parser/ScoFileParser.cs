using System;
using System.Collections.Generic;
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
        return Parse(filePath, relativePath, out _);
    }

    public OmsiAsset Parse(string filePath, string relativePath, out IReadOnlyList<string> warnings)
    {
        var scoFile = _parser.ParseFile(filePath);
        warnings = scoFile.Warnings;

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
            ModelReferences = scoFile.Meshes.Select(m => new OmsiModelReference(m.MeshPath)
            {
                Transform = new OmsiMeshTransform
                {
                    PosX = m.PosX,
                    PosY = m.PosY,
                    PosZ = m.PosZ,
                    RotX = m.RotX,
                    RotY = m.RotY,
                    RotZ = m.RotZ,
                    ScaleX = m.ScaleX,
                    ScaleY = m.ScaleY,
                    ScaleZ = m.ScaleZ
                },
                TransformWarnings = m.Warnings
            }).ToList(),
            TextureReferences = scoFile.TextureReferences
        };
    }
}
