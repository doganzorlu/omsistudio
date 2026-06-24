namespace OmsiStudio.Core.Assets;

public sealed class OmsiModelReference
{
    public string MeshPath { get; init; } = string.Empty;
    public string ResolvedPath { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public OmsiModelReferenceResolutionStatus ResolutionStatus { get; init; } = OmsiModelReferenceResolutionStatus.Unknown;

    public OmsiModelReference()
    {
    }

    public OmsiModelReference(string meshPath)
    {
        MeshPath = meshPath;
    }

    public OmsiModelReference(string meshPath, string resolvedPath, bool exists, OmsiModelReferenceResolutionStatus resolutionStatus)
    {
        MeshPath = meshPath;
        ResolvedPath = resolvedPath;
        Exists = exists;
        ResolutionStatus = resolutionStatus;
    }
}

