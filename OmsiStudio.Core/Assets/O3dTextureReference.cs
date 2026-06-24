namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents a texture reference embedded inside the O3D model file metadata.
/// </summary>
public sealed record O3dTextureReference
{
    /// <summary>
    /// Gets the texture path or reference string parsed from the O3D metadata.
    /// </summary>
    public string Path { get; init; } = string.Empty;
}
