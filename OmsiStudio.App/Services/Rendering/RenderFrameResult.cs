namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// Represents the result of a single frame rendering pass.
/// </summary>
public sealed record RenderFrameResult
{
    /// <summary>
    /// Gets a value indicating whether the frame rendered successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message if rendering failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful render frame result.
    /// </summary>
    public static RenderFrameResult Success() => new() { IsSuccess = true };

    /// <summary>
    /// Creates a failed render frame result.
    /// </summary>
    public static RenderFrameResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}
