namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents a vertex in a 3D model containing position, normal, and texture coordinates.
/// </summary>
public sealed record O3dVertex
{
    /// <summary>
    /// Gets the X position coordinate of the vertex.
    /// </summary>
    public float X { get; init; }

    /// <summary>
    /// Gets the Y position coordinate of the vertex.
    /// </summary>
    public float Y { get; init; }

    /// <summary>
    /// Gets the Z position coordinate of the vertex.
    /// </summary>
    public float Z { get; init; }

    /// <summary>
    /// Gets the normal vector of the vertex.
    /// </summary>
    public O3dNormal Normal { get; init; } = new();

    /// <summary>
    /// Gets the texture coordinates (UV) of the vertex.
    /// </summary>
    public O3dUv Uv { get; init; } = new();
}
