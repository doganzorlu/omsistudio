namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the interactive camera state for the preview viewport.
/// </summary>
public sealed record PreviewCameraState
{
    /// <summary>
    /// Gets the yaw angle of the camera orbit in degrees.
    /// </summary>
    public float Yaw { get; init; } = 45f;

    /// <summary>
    /// Gets the pitch angle of the camera orbit in degrees.
    /// </summary>
    public float Pitch { get; init; } = -30f;

    /// <summary>
    /// Gets the distance/zoom level of the camera from the target.
    /// </summary>
    public float Distance { get; init; } = 5f;

    /// <summary>
    /// Gets the pan offset vector.
    /// </summary>
    public PreviewVector3D PanOffset { get; init; } = new();
}
