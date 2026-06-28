namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the axis-aligned bounding box (AABB) bounds of a mesh.
/// </summary>
public sealed record MeshBounds
{
    /// <summary>
    /// Gets the minimum bounding coordinate of the AABB.
    /// </summary>
    public PreviewVector3D Min { get; init; } = new();

    /// <summary>
    /// Gets the maximum bounding coordinate of the AABB.
    /// </summary>
    public PreviewVector3D Max { get; init; } = new();

    /// <summary>
    /// Gets the center point coordinate of the AABB.
    /// </summary>
    public PreviewVector3D Center { get; init; } = new();

    /// <summary>
    /// Gets the total size/extents of the AABB across X, Y, and Z axes.
    /// </summary>
    public PreviewVector3D Size { get; init; } = new();
}
