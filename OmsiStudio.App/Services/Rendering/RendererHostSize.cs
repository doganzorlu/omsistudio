namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// Represents the viewport dimensions for the renderer host.
/// </summary>
public sealed record RendererHostSize
{
    /// <summary>
    /// Gets the width of the viewport.
    /// </summary>
    public double Width { get; init; }

    /// <summary>
    /// Gets the height of the viewport.
    /// </summary>
    public double Height { get; init; }
}
