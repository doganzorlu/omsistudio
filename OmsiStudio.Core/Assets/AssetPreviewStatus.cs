namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the status of the asset preview loading/rendering process.
/// </summary>
public enum AssetPreviewStatus
{
    /// <summary>
    /// Unknown status. Default.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// No asset has been requested for preview yet.
    /// </summary>
    Idle,

    /// <summary>
    /// Preview is currently loading.
    /// </summary>
    Loading,

    /// <summary>
    /// Preview has loaded successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The referenced model file is missing.
    /// </summary>
    Missing,

    /// <summary>
    /// The model format version is unsupported.
    /// </summary>
    Unsupported,

    /// <summary>
    /// The model file is encrypted.
    /// </summary>
    Encrypted,

    /// <summary>
    /// The model file geometry is invalid or corrupted.
    /// </summary>
    Invalid,

    /// <summary>
    /// The preview load failed due to general read or runtime error.
    /// </summary>
    Failed,

    /// <summary>
    /// The preview loading process was cancelled.
    /// </summary>
    Cancelled
}
