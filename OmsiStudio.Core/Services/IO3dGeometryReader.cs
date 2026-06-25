using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

/// <summary>
/// Defines the contract for parsing O3D model geometry data asynchronously.
/// </summary>
public interface IO3dGeometryReader
{
    /// <summary>
    /// Reads and parses version, encryption, vertex, face, and material geometry data from the specified O3D file asynchronously.
    /// </summary>
    /// <param name="filePath">The absolute path to the O3D model file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous read operation, containing the O3D geometry read result.</returns>
    Task<O3dGeometryReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
