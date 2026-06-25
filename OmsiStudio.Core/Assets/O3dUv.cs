namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents a 2D texture coordinate (UV) in a 3D model.
/// </summary>
public sealed record O3dUv
{
    /// <summary>
    /// Gets the horizontal texture coordinate.
    /// </summary>
    public float U { get; init; }

    /// <summary>
    /// Gets the vertical texture coordinate.
    /// </summary>
    public float V { get; init; }
}
