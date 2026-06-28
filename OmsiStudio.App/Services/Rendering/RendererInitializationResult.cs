namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// Represents the result of a renderer initialization attempt.
/// </summary>
public sealed record RendererInitializationResult
{
    /// <summary>
    /// Gets a value indicating whether the initialization was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message if the initialization failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful initialization result.
    /// </summary>
    public static RendererInitializationResult Success() => new() { IsSuccess = true };

    /// <summary>
    /// Creates a failed initialization result.
    /// </summary>
    public static RendererInitializationResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}
