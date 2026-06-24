namespace OmsiStudio.OmsiFormat.Sco;

public sealed class ScoMeshReference
{
    public string MeshPath { get; init; } = string.Empty;

    public ScoMeshReference()
    {
    }

    public ScoMeshReference(string meshPath)
    {
        MeshPath = meshPath;
    }
}
