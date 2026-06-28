namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the rendering settings and inspection options for the 3D viewport.
/// </summary>
public sealed record PreviewRenderOptions
{
    /// <summary>
    /// Gets a value indicating whether the mesh should be rendered in wireframe mode.
    /// </summary>
    public bool WireframeEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the axis-aligned bounding box should be rendered.
    /// </summary>
    public bool BoundingBoxEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether textures and materials should be loaded and rendered.
    /// </summary>
    public bool MaterialPreviewEnabled { get; init; } = true;
}
