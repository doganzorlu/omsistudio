using System.Collections.Generic;

namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the complete parsed mesh geometry data of an O3D model.
/// </summary>
public sealed record O3dMeshData
{
    /// <summary>
    /// Gets the vertices of the mesh.
    /// </summary>
    public IReadOnlyList<O3dVertex> Vertices { get; init; } = [];

    /// <summary>
    /// Gets the triangles/faces of the mesh.
    /// </summary>
    public IReadOnlyList<O3dTriangle> Triangles { get; init; } = [];

    /// <summary>
    /// Gets the material slots defined for the mesh.
    /// </summary>
    public IReadOnlyList<O3dMaterialSlot> MaterialSlots { get; init; } = [];

    /// <summary>
    /// Gets the optional O3D header/metadata associated with this mesh.
    /// </summary>
    public O3dMetadata? Metadata { get; init; }
}
