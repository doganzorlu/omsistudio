using System.Collections.Generic;
using System.Globalization;

namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the parsed metadata and statistics of an O3D model file.
/// </summary>
public sealed record O3dMetadata
{
    /// <summary>
    /// Gets the format version of the O3D file.
    /// </summary>
    public O3dFormatVersion Version { get; init; } = O3dFormatVersion.Unknown;

    /// <summary>
    /// Gets the raw numeric O3D version read from the file header, or 0 when it is unknown.
    /// </summary>
    public int RawVersion { get; init; }

    /// <summary>
    /// Gets a UI-friendly version string that preserves real O3D numeric versions.
    /// </summary>
    public string DisplayVersion => RawVersion > 0
        ? RawVersion.ToString(CultureInfo.InvariantCulture)
        : Version.ToString();

    /// <summary>
    /// Gets a value indicating whether the model file is encrypted.
    /// </summary>
    public bool IsEncrypted { get; init; }

    /// <summary>
    /// Gets the mesh or submesh count defined in the O3D header.
    /// </summary>
    public int MeshCount { get; init; }

    /// <summary>
    /// Gets the total vertex count defined in the O3D header.
    /// </summary>
    public int VertexCount { get; init; }

    /// <summary>
    /// Gets the total triangle/face count defined in the O3D header.
    /// </summary>
    public int TriangleCount { get; init; }

    /// <summary>
    /// Gets the material slot count defined in the O3D header.
    /// </summary>
    public int MaterialCount { get; init; }

    /// <summary>
    /// Gets the list of embedded texture reference strings parsed from materials.
    /// </summary>
    public IReadOnlyList<O3dTextureReference> TextureReferences { get; init; } = [];
}
