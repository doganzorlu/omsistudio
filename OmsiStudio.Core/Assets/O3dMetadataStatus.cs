namespace OmsiStudio.Core.Assets;

/// <summary>
/// Specifies the reading result status of O3D model metadata.
/// </summary>
public enum O3dMetadataStatus
{
    /// <summary>
    /// Unrecognized metadata read status.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Metadata was read successfully.
    /// </summary>
    Success = 1,

    /// <summary>
    /// The O3D format version is unsupported by the reader.
    /// </summary>
    Unsupported = 2,

    /// <summary>
    /// The O3D file is encrypted and cannot be parsed without decryption keys.
    /// </summary>
    Encrypted = 3,

    /// <summary>
    /// The O3D metadata headers are invalid or corrupted.
    /// </summary>
    Invalid = 4,

    /// <summary>
    /// The metadata read failed due to file system or structural reader failures.
    /// </summary>
    Failed = 5
}
