using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// Service contract for caching decoded texture images in memory to prevent duplicate load/decode operations.
/// </summary>
public interface ITextureImageCacheService
{
    /// <summary>
    /// Returns the cached texture result or loads it using the underlying loader if missing.
    /// </summary>
    Task<TextureLoadResult> GetOrLoadAsync(string resolvedPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    void Clear();
}
