namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents placement and transformation parameters (translation, rotation, scale) for a model reference.
/// </summary>
public sealed record OmsiMeshTransform
{
    /// <summary>
    /// Gets the translation offset along the X axis (in meters).
    /// </summary>
    public double PosX { get; init; } = 0.0;

    /// <summary>
    /// Gets the translation offset along the Y axis (in meters).
    /// </summary>
    public double PosY { get; init; } = 0.0;

    /// <summary>
    /// Gets the translation offset along the Z axis (in meters).
    /// </summary>
    public double PosZ { get; init; } = 0.0;

    /// <summary>
    /// Gets the rotation angle around the X axis (in degrees).
    /// </summary>
    public double RotX { get; init; } = 0.0;

    /// <summary>
    /// Gets the rotation angle around the Y axis (in degrees).
    /// </summary>
    public double RotY { get; init; } = 0.0;

    /// <summary>
    /// Gets the rotation angle around the Z axis (in degrees).
    /// </summary>
    public double RotZ { get; init; } = 0.0;

    /// <summary>
    /// Gets the scale factor along the X axis.
    /// </summary>
    public double ScaleX { get; init; } = 1.0;

    /// <summary>
    /// Gets the scale factor along the Y axis.
    /// </summary>
    public double ScaleY { get; init; } = 1.0;

    /// <summary>
    /// Gets the scale factor along the Z axis.
    /// </summary>
    public double ScaleZ { get; init; } = 1.0;

    /// <summary>
    /// Gets the default identity transform.
    /// </summary>
    public static OmsiMeshTransform Identity { get; } = new();
}
