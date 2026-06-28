using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.Core.Assets;
using Xunit;
using System.Linq;
namespace OmsiStudio.App.Tests;

public class TextureImageLoaderTests : IDisposable
{
    private readonly string _tempFileBmp;
    private readonly string _tempFilePng;
    private readonly string _tempFileJpeg;
    private readonly string _tempFileTga;
    private readonly string _tempFileDds;
    private readonly string _tempFileInvalid;
    private readonly string _tempFileTooLarge;

    public TextureImageLoaderTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "TextureImageLoaderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        _tempFileBmp = Path.Combine(tempDir, "test.bmp");
        _tempFilePng = Path.Combine(tempDir, "test.png");
        _tempFileJpeg = Path.Combine(tempDir, "test.jpg");
        _tempFileTga = Path.Combine(tempDir, "test.tga");
        _tempFileDds = Path.Combine(tempDir, "test.dds");
        _tempFileInvalid = Path.Combine(tempDir, "invalid.png");
        _tempFileTooLarge = Path.Combine(tempDir, "large.bmp");

        File.WriteAllBytes(_tempFileBmp, GenerateSyntheticBmp());
        File.WriteAllBytes(_tempFilePng, GenerateSyntheticPng());
        File.WriteAllBytes(_tempFileJpeg, GenerateSyntheticJpeg());
        File.WriteAllBytes(_tempFileTga, new byte[] { 1, 2, 3 }); // Dummy TGA content
        File.WriteAllBytes(_tempFileDds, new byte[] { 1, 2, 3 }); // Dummy DDS content
        File.WriteAllBytes(_tempFileInvalid, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00 }); // Bad PNG magic header content but corrupted
        File.WriteAllBytes(_tempFileTooLarge, GenerateSyntheticTooLargeBmp());
    }

    public void Dispose()
    {
        try
        {
            var dir = Path.GetDirectoryName(_tempFileBmp);
            if (dir != null && Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        catch
        {
            // Suppress cleanup exceptions
        }
    }

    [Fact]
    public async Task LoadAsync_SyntheticBmp_LoadsSuccessfully()
    {
        // Arrange
        var loader = new TextureImageLoader();

        // Act
        var result = await loader.LoadAsync(_tempFileBmp);

        // Assert
        Assert.Equal(TextureLoadStatus.Success, result.Status);
        Assert.NotNull(result.Image);
        Assert.Equal(1, result.Image.Width);
        Assert.Equal(1, result.Image.Height);
        Assert.Equal(TextureImageFormat.Bmp, result.Image.Format);
        Assert.Equal(4, result.Image.PixelsRgba32.Length); // 1x1 RGBA = 4 bytes
    }

    [Fact]
    public async Task LoadAsync_SyntheticPng_LoadsSuccessfully()
    {
        // Arrange
        var loader = new TextureImageLoader();

        // Act
        var result = await loader.LoadAsync(_tempFilePng);

        // Assert
        Assert.Equal(TextureLoadStatus.Success, result.Status);
        Assert.NotNull(result.Image);
        Assert.Equal(1, result.Image.Width);
        Assert.Equal(1, result.Image.Height);
        Assert.Equal(TextureImageFormat.Png, result.Image.Format);
        Assert.Equal(4, result.Image.PixelsRgba32.Length);
    }

    [Fact]
    public async Task LoadAsync_SyntheticJpeg_LoadsSuccessfully()
    {
        // Arrange
        var loader = new TextureImageLoader();

        // Act
        var result = await loader.LoadAsync(_tempFileJpeg);

        // Assert
        Assert.Equal(TextureLoadStatus.Success, result.Status);
        Assert.NotNull(result.Image);
        Assert.Equal(1, result.Image.Width);
        Assert.Equal(1, result.Image.Height);
        Assert.Equal(TextureImageFormat.Jpeg, result.Image.Format);
        Assert.Equal(4, result.Image.PixelsRgba32.Length);
    }

    [Fact]
    public async Task LoadAsync_TgaAndDds_ReturnsUnsupportedFormat()
    {
        // Arrange
        var loader = new TextureImageLoader();

        // Act
        var resultTga = await loader.LoadAsync(_tempFileTga);
        var resultDds = await loader.LoadAsync(_tempFileDds);

        // Assert
        Assert.Equal(TextureLoadStatus.UnsupportedFormat, resultTga.Status);
        Assert.Null(resultTga.Image);
        Assert.Single(resultTga.Diagnostics);

        Assert.Equal(TextureLoadStatus.UnsupportedFormat, resultDds.Status);
        Assert.Null(resultDds.Image);
        Assert.Single(resultDds.Diagnostics);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsFailed()
    {
        // Arrange
        var loader = new TextureImageLoader();

        // Act
        var result = await loader.LoadAsync("non_existent_file.png");

        // Assert
        Assert.Equal(TextureLoadStatus.Failed, result.Status);
        Assert.Null(result.Image);
        Assert.Single(result.Diagnostics);
        Assert.Contains("not found", result.Diagnostics[0].Message);
    }

    [Fact]
    public async Task LoadAsync_InvalidHeaderOrBytes_ReturnsInvalid()
    {
        // Arrange
        var loader = new TextureImageLoader();

        // Act
        var result = await loader.LoadAsync(_tempFileInvalid);

        // Assert
        Assert.Equal(TextureLoadStatus.Invalid, result.Status);
        Assert.Null(result.Image);
    }

    [Fact]
    public async Task LoadAsync_TooLargeDimensions_ReturnsTooLarge()
    {
        // Arrange
        var loader = new TextureImageLoader();

        // Act
        var result = await loader.LoadAsync(_tempFileTooLarge);

        // Assert
        Assert.Equal(TextureLoadStatus.TooLarge, result.Status);
        Assert.Null(result.Image);
        Assert.Single(result.Diagnostics);
        Assert.Contains("exceed maximum", result.Diagnostics[0].Message);
    }

    [Fact]
    public async Task LoadAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var loader = new TextureImageLoader();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => loader.LoadAsync(_tempFilePng, cts.Token));
    }

    [Fact]
    public async Task LoadAsync_FileSizeExceedsLimit_ReturnsTooLarge()
    {
        // Arrange
        var largeFile = Path.Combine(Path.GetTempPath(), "too_large_" + Guid.NewGuid().ToString("N") + ".bmp");
        using (var fs = File.Create(largeFile))
        {
            fs.SetLength(65 * 1024 * 1024); // 65 MB sparse file
        }

        try
        {
            var loader = new TextureImageLoader();

            // Act
            var result = await loader.LoadAsync(largeFile);

            // Assert
            Assert.Equal(TextureLoadStatus.TooLarge, result.Status);
            Assert.Contains("exceeds policy limit", result.Diagnostics[0].Message);
        }
        finally
        {
            if (File.Exists(largeFile))
            {
                File.Delete(largeFile);
            }
        }
    }

    private byte[] GenerateSyntheticBmp()
    {
        // 1x1 pixel 24-bit BMP
        byte[] bmp = new byte[58];
        // File Header
        bmp[0] = 0x42; bmp[1] = 0x4D; // "BM"
        BitConverter.GetBytes(58).CopyTo(bmp, 2); // File size
        BitConverter.GetBytes(54).CopyTo(bmp, 10); // Offset to pixel data
        // DIB Header
        BitConverter.GetBytes(40).CopyTo(bmp, 14); // Header size
        BitConverter.GetBytes(1).CopyTo(bmp, 18); // Width
        BitConverter.GetBytes(1).CopyTo(bmp, 22); // Height
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26); // Planes
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28); // Bits per pixel
        // Pixels (Blue, Green, Red + padding)
        bmp[54] = 0xFF; // Blue
        bmp[55] = 0x00; // Green
        bmp[56] = 0x00; // Red
        bmp[57] = 0x00; // Padding
        return bmp;
    }

    private byte[] GenerateSyntheticTooLargeBmp()
    {
        byte[] bmp = GenerateSyntheticBmp();
        BitConverter.GetBytes(5000).CopyTo(bmp, 18); // Width = 5000
        BitConverter.GetBytes(5000).CopyTo(bmp, 22); // Height = 5000
        return bmp;
    }

    private byte[] GenerateSyntheticPng()
    {
        string hex = "89504E470D0A1A0A0000000D4948445200000001000000010802000000907753DE0000000C49444154789C6360606000000004000127345B910000000049454E44AE426082";
        return Convert.FromHexString(hex);
    }

    private byte[] GenerateSyntheticJpeg()
    {
        string hex = "FFD8FFE000104A46494600010101006000600000FFDB004300080606070605080707070909080A0C140D0C0B0B0C1912130F141D1A1F1E1D1A1C1C20242E2720222C231C1C2837292C30313434341F27393D38323C2E333432FFC0000B080001000101011100FFC4001F0000010501010101010100000000000000000102030405060708090A0BFFC400B5100002010303020403050504040000017D01020300041105122131410613516107227114328191A1082342B1C11552D1F02433627282090A161718191A25262728292A3435363738393A434445464748494A535455565758595A636465666768696A737475767778797A838485868788898A92939495969798999A12131415161718191A232425262728292A32333435363738393A42434445464748494A52535455565758595A62636465666768696A72737475767778797A82838485868788898A92939495969798999AFFDA000C03010002110311003F00F7FA73FFD9";
        return Convert.FromHexString(hex);
    }
}
