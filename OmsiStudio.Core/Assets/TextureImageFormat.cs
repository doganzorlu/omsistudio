namespace OmsiStudio.Core.Assets;

/// <summary>
/// Specifies the supported image format of a texture.
/// </summary>
public enum TextureImageFormat
{
    /// <summary>
    /// Unknown or unrecognized format.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Windows Bitmap format (.bmp).
    /// </summary>
    Bmp,

    /// <summary>
    /// Portable Network Graphics format (.png).
    /// </summary>
    Png,

    /// <summary>
    /// JPEG Interchange Format (.jpg, .jpeg).
    /// </summary>
    Jpeg,

    /// <summary>
    /// Truevision TGA format (.tga).
    /// </summary>
    Tga,

    /// <summary>
    /// DirectDraw Surface format (.dds).
    /// </summary>
    Dds
}
