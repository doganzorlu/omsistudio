namespace OmsiStudio.Core.Assets;

/// <summary>
/// Specifies the load status outcome of a texture decoding operation.
/// </summary>
public enum TextureLoadStatus
{
    /// <summary>
    /// Unknown status.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Texture loaded and decoded successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The texture format is unsupported (e.g. DDS/TGA in early phase).
    /// </summary>
    UnsupportedFormat,

    /// <summary>
    /// The texture file header or payload is invalid/corrupt.
    /// </summary>
    Invalid,

    /// <summary>
    /// The texture width/height exceeds maximum permitted dimensions.
    /// </summary>
    TooLarge,

    /// <summary>
    /// General load or read failure (e.g. file missing, access denied).
    /// </summary>
    Failed
}
