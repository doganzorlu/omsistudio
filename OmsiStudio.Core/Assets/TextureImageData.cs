using System;

namespace OmsiStudio.Core.Assets;

/// <summary>
/// Represents the raw decoded pixel data of a texture image.
/// </summary>
public sealed record TextureImageData
{
    /// <summary>
    /// Gets the width of the image in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the height of the image in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the decoded texture format.
    /// </summary>
    public TextureImageFormat Format { get; init; } = TextureImageFormat.Unknown;

    /// <summary>
    /// Gets the raw 32-bit RGBA pixel array (width * height * 4 bytes).
    /// </summary>
    public byte[] PixelsRgba32 { get; init; } = Array.Empty<byte>();
}
