using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

public interface IOmsiModelReferenceResolver
{
    OmsiModelReference Resolve(string omsiRoot, string scoFilePath, string meshPath);
}
