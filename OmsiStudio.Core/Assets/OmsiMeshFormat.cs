using System;
using System.IO;

namespace OmsiStudio.Core.Assets;

/// <summary>
/// Specifies the supported or recognized mesh file formats.
/// </summary>
public enum OmsiMeshFormat
{
    /// <summary>
    /// File format is not supported or recognized.
    /// </summary>
    Unsupported,

    /// <summary>
    /// OMSI proprietary O3D binary format.
    /// </summary>
    O3d,

    /// <summary>
    /// DirectX .x mesh format.
    /// </summary>
    DirectX
}

/// <summary>
/// Helper utility to detect the mesh format from a file path.
/// </summary>
public static class OmsiMeshFormatHelper
{
    /// <summary>
    /// Detects the mesh format of the file at the given path based on its extension.
    /// </summary>
    public static OmsiMeshFormat DetectFormat(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return OmsiMeshFormat.Unsupported;
        }

        string ext = Path.GetExtension(path);
        if (ext.Equals(".o3d", StringComparison.OrdinalIgnoreCase))
        {
            return OmsiMeshFormat.O3d;
        }
        if (ext.Equals(".x", StringComparison.OrdinalIgnoreCase))
        {
            return OmsiMeshFormat.DirectX;
        }

        return OmsiMeshFormat.Unsupported;
    }
}
