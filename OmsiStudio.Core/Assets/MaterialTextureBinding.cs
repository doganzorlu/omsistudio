using System.Collections.Generic;

namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the mapping and resolution state of an O3D material slot to a decoded texture image.
/// </summary>
public sealed record MaterialTextureBinding
{
    /// <summary>
    /// Gets the zero-based index of the material slot in the mesh data.
    /// </summary>
    public int MaterialIndex { get; init; }

    /// <summary>
    /// Gets the name of the material.
    /// </summary>
    public string MaterialName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the original texture reference string.
    /// </summary>
    public string TextureReference { get; init; } = string.Empty;

    /// <summary>
    /// Gets the resolved texture path reference data, if resolved.
    /// </summary>
    public OmsiTextureReference? ResolvedTexture { get; init; }

    /// <summary>
    /// Gets the decoded pixel image data, if loaded.
    /// </summary>
    public TextureImageData? Image { get; init; }

    /// <summary>
    /// Gets the binding status outcome.
    /// </summary>
    public TextureBindingStatus Status { get; init; } = TextureBindingStatus.Unknown;

    /// <summary>
    /// Gets the list of diagnostics collected during resolution and loading.
    /// </summary>
    public IReadOnlyList<O3dDiagnostic> Diagnostics { get; init; } = [];
}
