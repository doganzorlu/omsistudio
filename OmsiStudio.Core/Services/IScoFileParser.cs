using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

public interface IScoFileParser
{
    OmsiAsset Parse(string filePath, string relativePath);
}
