namespace OmsiStudio.Core.Assets;

/// <summary>
/// Specifies the binding outcome status of an O3D material slot to its texture file.
/// </summary>
public enum TextureBindingStatus
{
    /// <summary>
    /// Unknown status.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Material slot was successfully resolved and texture pixels loaded.
    /// </summary>
    Bound,

    /// <summary>
    /// Texture reference is empty or resolved file is missing on disk.
    /// </summary>
    Missing,

    /// <summary>
    /// Texture format is unsupported.
    /// </summary>
    Unsupported,

    /// <summary>
    /// Texture reference is invalid or violates path traversal constraints.
    /// </summary>
    Invalid,

    /// <summary>
    /// Texture file length or dimensions exceed allowed policy constraints.
    /// </summary>
    TooLarge,

    /// <summary>
    /// Loading or decoding of texture failed.
    /// </summary>
    Failed
}
