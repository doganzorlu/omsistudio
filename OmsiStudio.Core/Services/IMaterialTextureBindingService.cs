using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

/// <summary>
/// Defines the contract for binding O3D material slots to decoded texture image payloads.
/// </summary>
public interface IMaterialTextureBindingService
{
    /// <summary>
    /// Binds each material slot of the mesh data to its resolved and decoded texture image.
    /// </summary>
    /// <param name="meshData">The parsed O3D mesh data.</param>
    /// <param name="modelFilePath">The absolute path to the O3D model file referencing the texture.</param>
    /// <param name="sceneryObjectsRoot">The absolute root directory containing Sceneryobjects.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A list of bindings corresponding to the material slots of the mesh.</returns>
    Task<IReadOnlyList<MaterialTextureBinding>> BindAsync(
        O3dMeshData meshData,
        string modelFilePath,
        string sceneryObjectsRoot,
        CancellationToken cancellationToken = default);
}
