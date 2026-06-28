using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

/// <summary>
/// Defines the contract for loading a 3D asset preview asynchronously from a selected asset/model reference.
/// </summary>
public interface IAssetPreviewLoader
{
    /// <summary>
    /// Loads the preview data (mesh data, computed bounds, and diagnostics) for the requested asset.
    /// </summary>
    /// <param name="request">The asset preview load request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous load operation.</param>
    /// <returns>A task representing the asynchronous load operation, containing the asset preview result.</returns>
    Task<AssetPreviewResult> LoadAsync(AssetPreviewRequest request, CancellationToken cancellationToken = default);
}
