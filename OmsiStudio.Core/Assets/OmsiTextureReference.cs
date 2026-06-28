namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents a resolved or unresolved texture reference belonging to a scenery object mesh.
/// </summary>
public sealed record OmsiTextureReference
{
    /// <summary>
    /// Gets the original texture path reference string (e.g. from O3D material slot).
    /// </summary>
    public string TexturePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the resolved absolute path to the texture file on the filesystem, if resolved.
    /// </summary>
    public string? ResolvedPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the texture file actually exists at the resolved path.
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    /// Gets the resolution status of this texture reference.
    /// </summary>
    public OmsiTextureReferenceResolutionStatus ResolutionStatus { get; init; } = OmsiTextureReferenceResolutionStatus.Unknown;
}
