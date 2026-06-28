using System.Collections.Generic;

namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the result of a texture load and decode operation.
/// </summary>
public sealed record TextureLoadResult
{
    /// <summary>
    /// Gets the decoded image data on success.
    /// </summary>
    public TextureImageData? Image { get; init; }

    /// <summary>
    /// Gets the final status of the load operation.
    /// </summary>
    public TextureLoadStatus Status { get; init; } = TextureLoadStatus.Unknown;

    /// <summary>
    /// Gets the diagnostics associated with the load process.
    /// </summary>
    public IReadOnlyList<O3dDiagnostic> Diagnostics { get; init; } = [];
}
