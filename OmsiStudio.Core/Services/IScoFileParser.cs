using System.Collections.Generic;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

public interface IScoFileParser
{
    OmsiAsset Parse(string filePath, string relativePath);
    OmsiAsset Parse(string filePath, string relativePath, out IReadOnlyList<string> warnings);
}
