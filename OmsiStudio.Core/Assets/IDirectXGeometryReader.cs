using System.Threading;
using System.Threading.Tasks;

namespace OmsiStudio.Core.Assets;

/// <summary>
/// Defines the contract for parsing DirectX .x mesh geometry files.
/// </summary>
public interface IDirectXGeometryReader
{
    /// <summary>
    /// Asynchronously reads DirectX .x mesh geometry from the given file.
    /// </summary>
    Task<O3dGeometryReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
