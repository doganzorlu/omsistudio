using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

/// <summary>
/// Defines the contract for resolving texture references of O3D models within OMSI directories.
/// </summary>
public interface IOmsiTextureReferenceResolver
{
    /// <summary>
    /// Resolves the absolute path of a texture reference.
    /// </summary>
    /// <param name="texturePath">The texture path reference string (e.g. from O3D material slot).</param>
    /// <param name="modelFilePath">The absolute path to the O3D model file referencing the texture.</param>
    /// <param name="sceneryObjectsRoot">The absolute root directory containing Sceneryobjects.</param>
    /// <returns>An <see cref="OmsiTextureReference"/> containing path resolution outcomes.</returns>
    OmsiTextureReference Resolve(string texturePath, string modelFilePath, string sceneryObjectsRoot);
}
