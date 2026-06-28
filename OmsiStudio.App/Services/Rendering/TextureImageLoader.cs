using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;
using StbImageSharp;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// A concrete implementation of <see cref="ITextureImageLoader"/> using the 100% managed StbImageSharp library.
/// </summary>
public class TextureImageLoader : ITextureImageLoader
{
    private const int MaxTextureWidth = 4096;
    private const int MaxTextureHeight = 4096;
    private const long MaxTextureFileBytes = 64 * 1024 * 1024; // 64 MB guardrail

    /// <inheritdoc />
    public async Task<TextureLoadResult> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new TextureLoadResult
            {
                Status = TextureLoadStatus.Failed,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Message = "Texture path is empty." }]
            };
        }

        // 1. Verify file existence
        if (!File.Exists(filePath))
        {
            return new TextureLoadResult
            {
                Status = TextureLoadStatus.Failed,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Warning, Message = $"Texture file not found: {filePath}" }]
            };
        }

        // 2. Detect unsupported formats by file extension
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".tga" || extension == ".dds")
        {
            return new TextureLoadResult
            {
                Status = TextureLoadStatus.UnsupportedFormat,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Warning, Message = $"Texture format {extension} is not supported in this phase." }]
            };
        }

        // Detect supported format
        TextureImageFormat imageFormat = extension switch
        {
            ".bmp" => TextureImageFormat.Bmp,
            ".png" => TextureImageFormat.Png,
            ".jpg" or ".jpeg" => TextureImageFormat.Jpeg,
            _ => TextureImageFormat.Unknown
        };

        if (imageFormat == TextureImageFormat.Unknown)
        {
            return new TextureLoadResult
            {
                Status = TextureLoadStatus.UnsupportedFormat,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Warning, Message = $"Unsupported texture file extension: {extension}" }]
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                // A. Check file size guardrail before allocating buffer
                if (fs.Length > MaxTextureFileBytes)
                {
                    return new TextureLoadResult
                    {
                        Status = TextureLoadStatus.TooLarge,
                        Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Warning, Message = $"Texture file size {fs.Length} bytes exceeds policy limit ({MaxTextureFileBytes} bytes)." }]
                    };
                }

                cancellationToken.ThrowIfCancellationRequested();

                // B. Read header bytes (first 4 bytes) for magic validation
                if (fs.Length < 4)
                {
                    return new TextureLoadResult
                    {
                        Status = TextureLoadStatus.Invalid,
                        Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Message = "Corrupted texture file: payload is too small." }]
                    };
                }

                byte[] headerBytes = new byte[4];
                int bytesRead = await fs.ReadAsync(headerBytes, 0, 4, cancellationToken);
                if (bytesRead < 4)
                {
                    return new TextureLoadResult
                    {
                        Status = TextureLoadStatus.Invalid,
                        Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Message = "Corrupted texture file: could not read magic header." }]
                    };
                }

                // Verify formats by checking magic headers
                // PNG magic: 89 50 4E 47
                // BMP magic: 42 4D (BM)
                // JPEG magic: FF D8
                bool magicIsValid = imageFormat switch
                {
                    TextureImageFormat.Png => headerBytes[0] == 0x89 && headerBytes[1] == 0x50 && headerBytes[2] == 0x4E && headerBytes[3] == 0x47,
                    TextureImageFormat.Bmp => headerBytes[0] == 0x42 && headerBytes[1] == 0x4D,
                    TextureImageFormat.Jpeg => headerBytes[0] == 0xFF && headerBytes[1] == 0xD8,
                    _ => false
                };

                if (!magicIsValid)
                {
                    return new TextureLoadResult
                    {
                        Status = TextureLoadStatus.Invalid,
                        Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Message = $"File header magic does not match expected format {imageFormat}." }]
                    };
                }

                cancellationToken.ThrowIfCancellationRequested();

                // C. Probe dimensions using stream seeking (lightweight header parsing)
                fs.Position = 0;
                var info = ImageInfo.FromStream(fs);
                if (info == null)
                {
                    return new TextureLoadResult
                    {
                        Status = TextureLoadStatus.Invalid,
                        Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Message = "Failed to parse image headers." }]
                    };
                }

                if (info.Value.Width > MaxTextureWidth || info.Value.Height > MaxTextureHeight)
                {
                    return new TextureLoadResult
                    {
                        Status = TextureLoadStatus.TooLarge,
                        Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Warning, Message = $"Texture dimensions {info.Value.Width}x{info.Value.Height} exceed maximum policy threshold ({MaxTextureWidth}x{MaxTextureHeight})." }]
                    };
                }

                cancellationToken.ThrowIfCancellationRequested();

                // D. All validation passed. Perform full decode from stream
                fs.Position = 0;
                var imageResult = ImageResult.FromStream(fs, ColorComponents.RedGreenBlueAlpha);
                if (imageResult == null || imageResult.Data == null)
                {
                    return new TextureLoadResult
                    {
                        Status = TextureLoadStatus.Invalid,
                        Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Message = "Failed to decode pixel data." }]
                    };
                }

                return new TextureLoadResult
                {
                    Status = TextureLoadStatus.Success,
                    Image = new TextureImageData
                    {
                        Width = imageResult.Width,
                        Height = imageResult.Height,
                        Format = imageFormat,
                        PixelsRgba32 = imageResult.Data
                    }
                };
            }
        }
        catch (OperationCanceledException)
        {
            throw; // propagate cancellation
        }
        catch (Exception ex)
        {
            return new TextureLoadResult
            {
                Status = TextureLoadStatus.Invalid,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Message = $"Failed to decode texture: {ex.Message}" }]
            };
        }
    }
}
