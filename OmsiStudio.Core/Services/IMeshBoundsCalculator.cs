using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

/// <summary>
/// Defines the contract for calculating the axis-aligned bounding box (AABB) bounds of a mesh.
/// </summary>
public interface IMeshBoundsCalculator
{
    /// <summary>
    /// Calculates the bounding box bounds for the specified mesh data.
    /// </summary>
    /// <param name="meshData">The mesh data containing vertices.</param>
    /// <returns>The computed mesh bounds.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when meshData is null.</exception>
    MeshBounds CalculateBounds(O3dMeshData meshData);
}
