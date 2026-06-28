namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// Represents the lifecycle states of the renderer host.
/// </summary>
public enum RendererHostState
{
    /// <summary>
    /// The renderer host has been created but not yet initialized.
    /// </summary>
    Uninitialized = 0,

    /// <summary>
    /// The renderer host has successfully initialized its graphics context.
    /// </summary>
    Initialized = 1,

    /// <summary>
    /// Initialization or rendering failed.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// The renderer host has been disposed and cannot be used.
    /// </summary>
    Disposed = 3
}
