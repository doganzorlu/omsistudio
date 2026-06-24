namespace OmsiStudio.Core.Assets;

public sealed class OmsiModelReference
{
    public string MeshPath { get; init; } = string.Empty;

    public OmsiModelReference()
    {
    }

    public OmsiModelReference(string meshPath)
    {
        MeshPath = meshPath;
    }
}
