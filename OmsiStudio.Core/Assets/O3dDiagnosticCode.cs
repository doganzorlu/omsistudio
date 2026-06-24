namespace OmsiStudio.Core.Assets;

/// <summary>
/// Specifies diagnostic codes for parser warnings, errors, and safety violations.
/// </summary>
public enum O3dDiagnosticCode
{
    /// <summary>
    /// Unrecognized diagnostic code.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The input binary stream is truncated before the expected section ended.
    /// </summary>
    TruncatedStream = 1,

    /// <summary>
    /// The O3D file version is unrecognized or unsupported.
    /// </summary>
    UnsupportedVersion = 2,

    /// <summary>
    /// The O3D file is encrypted/protected.
    /// </summary>
    EncryptedFile = 3,

    /// <summary>
    /// The file header metadata bytes are invalid.
    /// </summary>
    InvalidHeader = 4,

    /// <summary>
    /// Extracted element counts are negative or invalid.
    /// </summary>
    InvalidCount = 5,

    /// <summary>
    /// A length-prefixed string exceeds the allowed buffer boundaries.
    /// </summary>
    StringLengthExceeded = 6,

    /// <summary>
    /// String bounds check failed against the remaining stream length.
    /// </summary>
    InvalidStringBounds = 7,

    /// <summary>
    /// Generic binary reading operation failure.
    /// </summary>
    ReadFailed = 8,

    /// <summary>
    /// Allocation safety limits were exceeded based on untrusted counts.
    /// </summary>
    SafetyLimitExceeded = 9
}
