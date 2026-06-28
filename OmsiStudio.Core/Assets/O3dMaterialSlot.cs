namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents a material slot in an O3D model, detailing the material name and optionally its texture reference.
/// </summary>
public sealed record O3dMaterialSlot
{
    /// <summary>
    /// Gets the name of the material.
    /// </summary>
    public string MaterialName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional texture reference path associated with this material slot.
    /// </summary>
    public string? TextureReference { get; init; }

    /// <summary>
    /// Gets the optional source model file path from which this material slot was loaded.
    /// Used in multi-mesh scenes to locate/resolve the texture relative to the correct folder.
    /// </summary>
    public string? SourceModelPath { get; init; }
}
