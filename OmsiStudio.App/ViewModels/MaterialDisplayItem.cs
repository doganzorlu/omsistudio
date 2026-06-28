using System;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.ViewModels;

/// <summary>
/// A UI-safe display model representing a material slot with its deterministic color brush,
/// texture binding status, diagnostics, and small thumbnail preview.
/// </summary>
public sealed class MaterialDisplayItem
{
    /// <summary>
    /// Gets or sets a factory delegate for creating texture thumbnails.
    /// Useful for overriding thumbnail creation in headless unit test environments.
    /// </summary>
    public static Func<TextureImageData, IImage?>? ThumbnailFactory { get; set; }

    /// <summary>
    /// Gets the display name of the material.
    /// </summary>
    public string MaterialName { get; }

    /// <summary>
    /// Gets the texture reference path (or localized missing texture text).
    /// </summary>
    public string TextureReference { get; }

    /// <summary>
    /// Gets the solid color brush representing the material's deterministic preview color.
    /// </summary>
    public IBrush ColorBrush { get; }

    /// <summary>
    /// Gets the status string of the texture binding (Bound / Missing / Unsupported / Invalid / Failed).
    /// </summary>
    public string BindingStatus { get; } = string.Empty;

    /// <summary>
    /// Gets the resolved texture path, if resolved.
    /// </summary>
    public string ResolvedPath { get; } = string.Empty;

    /// <summary>
    /// Gets the decoded image size representation, e.g. "256x256".
    /// </summary>
    public string ImageSizeText { get; } = string.Empty;

    /// <summary>
    /// Gets the small texture thumbnail preview bitmap.
    /// </summary>
    public IImage? TextureThumbnail { get; }

    /// <summary>
    /// Gets a value indicating whether a thumbnail is loaded.
    /// </summary>
    public bool HasTextureThumbnail => TextureThumbnail != null;

    /// <summary>
    /// Gets a value indicating whether a resolved path exists.
    /// </summary>
    public bool HasResolvedPath => !string.IsNullOrEmpty(ResolvedPath);

    /// <summary>
    /// Gets a concatenated list of diagnostic messages for this material.
    /// </summary>
    public string DiagnosticsText { get; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether any diagnostics exist for this material.
    /// </summary>
    public bool HasDiagnostics => !string.IsNullOrEmpty(DiagnosticsText);

    /// <summary>
    /// Gets the foreground brush for the binding status text.
    /// </summary>
    public IBrush StatusForeground { get; } = Brushes.SlateGray;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialDisplayItem"/> class.
    /// </summary>
    public MaterialDisplayItem(string materialName, string textureReference, Color color, string notBoundText, MaterialTextureBinding? binding = null)
    {
        MaterialName = materialName;
        TextureReference = textureReference;
        ColorBrush = new SolidColorBrush(color);

        if (binding != null)
        {
            BindingStatus = binding.Status.ToString();
            ResolvedPath = binding.ResolvedTexture?.ResolvedPath ?? string.Empty;

            if (binding.Image != null)
            {
                ImageSizeText = $"{binding.Image.Width}x{binding.Image.Height}";
                TextureThumbnail = CreateThumbnail(binding.Image);
            }

            // Status color brush mapping
            StatusForeground = binding.Status switch
            {
                TextureBindingStatus.Bound => Brushes.LightGreen,
                TextureBindingStatus.Missing => Brushes.Orange,
                TextureBindingStatus.Unsupported => Brushes.DarkOrange,
                TextureBindingStatus.Invalid => Brushes.Red,
                TextureBindingStatus.Failed => Brushes.Crimson,
                _ => Brushes.SlateGray
            };

            // Diagnostics mapping
            if (binding.Diagnostics != null && binding.Diagnostics.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var diag in binding.Diagnostics)
                {
                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                    }
                    sb.Append(diag.Message);
                }
                DiagnosticsText = sb.ToString();
            }
        }
        else
        {
            BindingStatus = notBoundText;
            StatusForeground = Brushes.SlateGray;
        }
    }

    private static IImage? CreateThumbnail(TextureImageData? image)
    {
        if (image == null || image.PixelsRgba32 == null || image.Width <= 0 || image.Height <= 0)
        {
            return null;
        }

        try
        {
            if (ThumbnailFactory != null)
            {
                return ThumbnailFactory(image);
            }

            var bitmap = new WriteableBitmap(
                new PixelSize(image.Width, image.Height),
                new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormat.Rgba8888,
                Avalonia.Platform.AlphaFormat.Unpremul);

            using (var locked = bitmap.Lock())
            {
                System.Runtime.InteropServices.Marshal.Copy(image.PixelsRgba32, 0, locked.Address, image.PixelsRgba32.Length);
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
