using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

/// <summary>
/// Service that loads and decodes texture image files.
/// </summary>
public interface ITextureImageLoader
{
    /// <summary>
    /// Loads and decodes a texture image from the specified file path.
    /// </summary>
    /// <param name="filePath">The absolute path to the texture file.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A result containing status and decoded pixel details.</returns>
    Task<TextureLoadResult> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}
