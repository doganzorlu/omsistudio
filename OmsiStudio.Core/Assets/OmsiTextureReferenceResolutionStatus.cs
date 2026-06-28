namespace OmsiStudio.Core.Assets;

/// <summary>
/// Specifies the resolution status of an OMSI texture reference.
/// </summary>
public enum OmsiTextureReferenceResolutionStatus
{
    /// <summary>
    /// Unknown status.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The texture path was successfully resolved and the file exists.
    /// </summary>
    Resolved,

    /// <summary>
    /// The texture path resolved but the file does not exist.
    /// </summary>
    Missing,

    /// <summary>
    /// The texture reference format or resolution path is invalid (e.g. path traversal violation).
    /// </summary>
    InvalidPath
}
