namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents a structured diagnostic message produced during O3D parsing.
/// </summary>
public sealed record O3dDiagnostic
{
    /// <summary>
    /// Gets the severity of the diagnostic message.
    /// </summary>
    public O3dDiagnosticSeverity Severity { get; init; } = O3dDiagnosticSeverity.Unknown;

    /// <summary>
    /// Gets the specific diagnostic code.
    /// </summary>
    public O3dDiagnosticCode Code { get; init; } = O3dDiagnosticCode.Unknown;

    /// <summary>
    /// Gets the descriptive message context.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional byte offset in the stream where the diagnostic was triggered.
    /// </summary>
    public long? ByteOffset { get; init; }

    /// <summary>
    /// Gets the optional context information or variable state details.
    /// </summary>
    public string? Context { get; init; }
}
