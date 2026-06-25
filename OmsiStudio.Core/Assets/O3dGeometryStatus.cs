namespace OmsiStudio.Core.Assets;

/// <summary>
/// Specifies the reading result status of O3D model geometry.
/// </summary>
public enum O3dGeometryStatus
{
    /// <summary>
    /// Unrecognized geometry read status.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Geometry was read successfully.
    /// </summary>
    Success = 1,

    /// <summary>
    /// The O3D format version is unsupported by the geometry reader.
    /// </summary>
    Unsupported = 2,

    /// <summary>
    /// The O3D file is encrypted and cannot be parsed without decryption keys.
    /// </summary>
    Encrypted = 3,

    /// <summary>
    /// The O3D geometry data is invalid or corrupted.
    /// </summary>
    Invalid = 4,

    /// <summary>
    /// The geometry read failed due to file system or structural reader failures.
    /// </summary>
    Failed = 5
}
