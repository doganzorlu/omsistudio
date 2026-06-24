using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

/// <summary>
/// Defines the contract for parsing O3D model file header metadata asynchronously.
/// </summary>
public interface IO3dMetadataReader
{
    /// <summary>
    /// Reads and parses version, encryption, and count metadata from the specified O3D file asynchronously.
    /// </summary>
    /// <param name="filePath">The absolute path to the O3D model file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous read operation, containing the O3D metadata read result.</returns>
    Task<O3dMetadataReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
