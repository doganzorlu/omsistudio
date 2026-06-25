namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents a triangle/face in an O3D model, referencing three vertex indices.
/// </summary>
public sealed record O3dTriangle
{
    /// <summary>
    /// Gets the index of the first vertex.
    /// </summary>
    public int V0 { get; init; }

    /// <summary>
    /// Gets the index of the second vertex.
    /// </summary>
    public int V1 { get; init; }

    /// <summary>
    /// Gets the index of the third vertex.
    /// </summary>
    public int V2 { get; init; }

    /// <summary>
    /// Gets the optional index of the material slot assigned to this triangle.
    /// </summary>
    public int? MaterialSlotIndex { get; init; }
}
