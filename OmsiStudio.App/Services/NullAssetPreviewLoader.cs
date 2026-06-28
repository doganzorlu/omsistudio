using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.App.Services;

/// <summary>
/// A null implementation of IAssetPreviewLoader used when no real renderer or loader is registered.
/// </summary>
public class NullAssetPreviewLoader : IAssetPreviewLoader
{
    /// <summary>
    /// Returns an idle preview result without reading any file.
    /// </summary>
    public Task<AssetPreviewResult> LoadAsync(AssetPreviewRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AssetPreviewResult
        {
            Status = AssetPreviewStatus.Idle,
            Diagnostics = []
        });
    }
}
