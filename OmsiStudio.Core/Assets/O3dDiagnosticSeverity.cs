namespace OmsiStudio.Core.Assets;

/// <summary>
/// Specifies the severity level of O3D model parsing diagnostics.
/// </summary>
public enum O3dDiagnosticSeverity
{
    /// <summary>
    /// Unrecognized diagnostic severity level.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Informational message generated during parsing.
    /// </summary>
    Info = 1,

    /// <summary>
    /// Non-fatal warning indicating potential formatting issues.
    /// </summary>
    Warning = 2,

    /// <summary>
    /// Fatal error indicating structural corruption or parsing failure.
    /// </summary>
    Error = 3
}
