namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents a 3D normal vector in a 3D model.
/// </summary>
public sealed record O3dNormal
{
    /// <summary>
    /// Gets the X component of the normal vector.
    /// </summary>
    public float X { get; init; }

    /// <summary>
    /// Gets the Y component of the normal vector.
    /// </summary>
    public float Y { get; init; }

    /// <summary>
    /// Gets the Z component of the normal vector.
    /// </summary>
    public float Z { get; init; }
}
