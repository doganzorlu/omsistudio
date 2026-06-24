namespace OmsiStudio.Core.Assets;

/// <summary>
/// Specifies the O3D model file format version.
/// </summary>
public enum O3dFormatVersion
{
    /// <summary>
    /// Unrecognized format version.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Legacy version of O3D format (Version < 3) utilizing short headers.
    /// </summary>
    Legacy = 1,

    /// <summary>
    /// Version 3 of O3D format utilizing long headers.
    /// </summary>
    Version3 = 2,

    /// <summary>
    /// Version 4 of O3D format utilizing long headers.
    /// </summary>
    Version4 = 3
}
