namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents a 3D vector or point in the preview domain.
/// </summary>
public sealed record PreviewVector3D
{
    /// <summary>
    /// Gets the X component of the 3D vector.
    /// </summary>
    public float X { get; init; }

    /// <summary>
    /// Gets the Y component of the 3D vector.
    /// </summary>
    public float Y { get; init; }

    /// <summary>
    /// Gets the Z component of the 3D vector.
    /// </summary>
    public float Z { get; init; }
}
