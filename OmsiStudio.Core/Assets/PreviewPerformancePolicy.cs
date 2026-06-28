namespace OmsiStudio.Core.Assets;

/// <summary>
/// Central performance guardrails policy configuration for realistic scenery object previews.
/// </summary>
public static class PreviewPerformancePolicy
{
    /// <summary>
    /// Gets or sets the maximum allowed vertices in a preview mesh.
    /// </summary>
    public static int MaxPreviewVertices { get; set; } = 100000;

    /// <summary>
    /// Gets or sets the maximum allowed triangles in a preview mesh.
    /// </summary>
    public static int MaxPreviewTriangles { get; set; } = 100000;

    /// <summary>
    /// Gets or sets the maximum allowed material slots in a preview mesh.
    /// </summary>
    public static int MaxPreviewMaterials { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum allowed texture bindings for the preview.
    /// </summary>
    public static int MaxTextureBindings { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum allowed total decoded texture pixels (sum of width * height across all bound textures).
    /// </summary>
    public static long MaxTotalTexturePixels { get; set; } = 8192 * 8192; // 67,108,864 pixels

    /// <summary>
    /// Gets or sets the maximum allowed viewport rasterizer pixel count (width * height) to prevent huge CPU memory allocations.
    /// </summary>
    public static int MaxViewportRasterPixels { get; set; } = 3840 * 2160; // 4K resolution (8,294,400 pixels)
}
