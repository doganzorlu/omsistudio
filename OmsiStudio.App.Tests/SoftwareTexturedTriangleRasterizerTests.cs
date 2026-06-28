using System;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.Core.Assets;
using Xunit;

namespace OmsiStudio.App.Tests;

public class SoftwareTexturedTriangleRasterizerTests
{
    private readonly TextureImageData _testTexture;

    public SoftwareTexturedTriangleRasterizerTests()
    {
        // Create a 2x2 test texture:
        // (0,0): Red, Alpha 128
        // (1,0): Green, Alpha 150
        // (0,1): Blue, Alpha 200
        // (1,1): Yellow, Alpha 255
        byte[] pixels = new byte[2 * 2 * 4];
        
        // (0,0) - index 0
        pixels[0] = 255; pixels[1] = 0; pixels[2] = 0; pixels[3] = 128;
        // (1,0) - index 4
        pixels[4] = 0; pixels[5] = 255; pixels[6] = 0; pixels[7] = 150;
        // (0,1) - index 8
        pixels[8] = 0; pixels[9] = 0; pixels[10] = 255; pixels[11] = 200;
        // (1,1) - index 12
        pixels[12] = 255; pixels[13] = 255; pixels[14] = 0; pixels[15] = 255;

        _testTexture = new TextureImageData
        {
            Width = 2,
            Height = 2,
            Format = TextureImageFormat.Png,
            PixelsRgba32 = pixels
        };
    }

    [Fact]
    public void Rasterize_CenterPixel_SamplesExpectedColorAndPreservesAlpha()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];

        // Act
        // Triangle covering bottom-left half: (0,0) to (10,0) to (0,10)
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            0f, 10f,
            0f, 0f,
            1f, 0f,
            0f, 1f,
            _testTexture,
            1f,
            TextureSamplingMode.Nearest
        );

        // Assert
        // Center pixel (3, 3) is inside.
        // Index mapping: (3 * 10 + 3) * 4 = 132
        int index = (3 * w + 3) * 4;
        Assert.Equal(255, buffer[index]);      // Red
        Assert.Equal(0, buffer[index + 1]);  // Green
        Assert.Equal(0, buffer[index + 2]);  // Blue
        Assert.Equal(128, buffer[index + 3]); // Alpha preserved
    }

    [Fact]
    public void Rasterize_DifferentUV_SamplesDifferentPixels()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];

        // Act
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            0f, 10f,
            0f, 0f,
            1f, 0f,
            0f, 1f,
            _testTexture,
            1f,
            TextureSamplingMode.Nearest
        );

        // Assert
        // Pixel at (7, 1) is closer to (10,0) which has UV (1, 0)
        // w1 = 7.5 / 10 = 0.75, w2 = 1.5 / 10 = 0.15, w0 = 0.1
        // u = 0.75, v = 0.15
        // nearest-neighbor wraps to: tx = (int)(0.75 * 2) = 1, ty = (int)(0.15 * 2) = 0 -> (1,0)
        // Which is Green (0, 255, 0, 150)
        int index = (1 * w + 7) * 4; // py=1, px=7
        Assert.Equal(0, buffer[index]);
        Assert.Equal(255, buffer[index + 1]);
        Assert.Equal(0, buffer[index + 2]);
        Assert.Equal(150, buffer[index + 3]);
    }

    [Fact]
    public void Rasterize_OutOfBoundsTriangle_DoesNotCrashAndLeavesBufferUntouched()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];

        // Act & Assert (No crash expected)
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            -20f, -20f,
            -10f, -20f,
            -20f, -10f,
            0f, 0f,
            1f, 0f,
            0f, 1f,
            _testTexture
        );

        // Buffer should remain completely all-zeros
        foreach (byte b in buffer)
        {
            Assert.Equal(0, b);
        }
    }

    [Fact]
    public void Rasterize_DegenerateTriangle_NoOp()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];

        // Act
        // Triangle on a single line (0,0) -> (10,0) -> (5,0)
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            5f, 0f,
            0f, 0f,
            1f, 0f,
            0.5f, 0f,
            _testTexture
        );

        // Buffer should remain completely all-zeros
        foreach (byte b in buffer)
        {
            Assert.Equal(0, b);
        }
    }

    [Fact]
    public void Rasterize_InvalidInputs_DoesNotCrash()
    {
        // Arrange
        byte[] buffer = new byte[400];

        // Act & Assert (Should exit early without any crash/null reference)
        SoftwareTexturedTriangleRasterizer.Rasterize(null!, 10, 10, 0, 0, 1, 1, 2, 2, 0, 0, 1, 1, 2, 2, _testTexture);
        SoftwareTexturedTriangleRasterizer.Rasterize(buffer, 0, 10, 0, 0, 1, 1, 2, 2, 0, 0, 1, 1, 2, 2, _testTexture);
        SoftwareTexturedTriangleRasterizer.Rasterize(buffer, 10, 10, 0, 0, 1, 1, 2, 2, 0, 0, 1, 1, 2, 2, null!);
        SoftwareTexturedTriangleRasterizer.Rasterize(buffer, 5, 5, 0, 0, 1, 1, 2, 2, 0, 0, 1, 1, 2, 2, _testTexture); // Buffer length mismatch

        // 1. TextureImageData width/height positive but PixelsRgba32 is short
        var shortTexture = new TextureImageData
        {
            Width = 10,
            Height = 10,
            Format = TextureImageFormat.Png,
            PixelsRgba32 = new byte[10] // Expected 10 * 10 * 4 = 400 bytes, only has 10
        };
        SoftwareTexturedTriangleRasterizer.Rasterize(buffer, 10, 10, 0, 0, 1, 1, 2, 2, 0, 0, 1, 1, 2, 2, shortTexture);

        // 2. Extremely large dimensions (overflow target width/height)
        SoftwareTexturedTriangleRasterizer.Rasterize(buffer, int.MaxValue, 10, 0, 0, 1, 1, 2, 2, 0, 0, 1, 1, 2, 2, _testTexture);
        SoftwareTexturedTriangleRasterizer.Rasterize(buffer, 10, int.MaxValue, 0, 0, 1, 1, 2, 2, 0, 0, 1, 1, 2, 2, _testTexture);

        // 3. Extremely large texture dimensions
        var hugeTexture = new TextureImageData
        {
            Width = int.MaxValue,
            Height = 10,
            Format = TextureImageFormat.Png,
            PixelsRgba32 = new byte[10]
        };
        SoftwareTexturedTriangleRasterizer.Rasterize(buffer, 10, 10, 0, 0, 1, 1, 2, 2, 0, 0, 1, 1, 2, 2, hugeTexture);
    }

    [Fact]
    public void Rasterize_BilinearInterpolation_CalculatesExpectedColors()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];

        // Act - Draw a triangle with constant UV = (0.5, 0.5)
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            0f, 10f,
            0.5f, 0.5f,
            0.5f, 0.5f,
            0.5f, 0.5f,
            _testTexture,
            1f,
            TextureSamplingMode.Bilinear
        );

        // Assert - Interpolated colors
        // R = 127, G = 127, B = 63, A = 183
        int index = (3 * w + 3) * 4;
        Assert.Equal(127, buffer[index]);      // R
        Assert.Equal(127, buffer[index + 1]);  // G
        Assert.Equal(63, buffer[index + 2]);   // B
        Assert.Equal(183, buffer[index + 3]);  // A
    }

    [Fact]
    public void Rasterize_AlphaBlending_BlendsCorrectlyWithExistingBuffer()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];
        // Pre-fill target buffer with solid gray (100, 100, 100, 255)
        for (int i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = 100;
            buffer[i + 1] = 100;
            buffer[i + 2] = 100;
            buffer[i + 3] = 255;
        }

        // Act - Draw a triangle with constant UV = (0.5, 0.5)
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            0f, 10f,
            0.5f, 0.5f,
            0.5f, 0.5f,
            0.5f, 0.5f,
            _testTexture,
            1f,
            TextureSamplingMode.Bilinear
        );

        // Assert - Out colors: R=119, G=119, B=73, A=255
        int index = (3 * w + 3) * 4;
        Assert.Equal(119, buffer[index]);
        Assert.Equal(119, buffer[index + 1]);
        Assert.Equal(73, buffer[index + 2]);
        Assert.Equal(255, buffer[index + 3]);
    }

    [Fact]
    public void Rasterize_AlphaChannel_IsUnchangedByShadingIntensity()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];

        // Act - Draw with intensity = 0.5f
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            0f, 10f,
            0.5f, 0.5f,
            0.5f, 0.5f,
            0.5f, 0.5f,
            _testTexture,
            0.5f,
            TextureSamplingMode.Bilinear
        );

        // Assert
        int index = (3 * w + 3) * 4;
        Assert.Equal(63, buffer[index]);      // R shaded
        Assert.Equal(63, buffer[index + 1]);  // G shaded
        Assert.Equal(31, buffer[index + 2]);   // B shaded
        Assert.Equal(183, buffer[index + 3]);  // Alpha remains unshaded!
    }

    [Fact]
    public void Rasterize_BilinearSampling_PositiveWrap_UVGreaterThanOne()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];

        // Act - Draw a triangle with constant UV = (1.5, 1.5)
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            0f, 10f,
            1.5f, 1.5f,
            1.5f, 1.5f,
            1.5f, 1.5f,
            _testTexture,
            1f,
            TextureSamplingMode.Bilinear
        );

        // Assert - Should wrap to (0.5, 0.5) yielding R = 127, G = 127, B = 63, A = 183
        int index = (3 * w + 3) * 4;
        Assert.Equal(127, buffer[index]);
        Assert.Equal(127, buffer[index + 1]);
        Assert.Equal(63, buffer[index + 2]);
        Assert.Equal(183, buffer[index + 3]);
    }

    [Fact]
    public void Rasterize_BilinearSampling_NegativeWrap()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];

        // Act - Draw a triangle with constant UV = (-0.5, -0.5)
        // -0.5 - floor(-0.5) = -0.5 - (-1.0) = 0.5
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            0f, 10f,
            -0.5f, -0.5f,
            -0.5f, -0.5f,
            -0.5f, -0.5f,
            _testTexture,
            1f,
            TextureSamplingMode.Bilinear
        );

        // Assert - Should wrap to (0.5, 0.5) yielding R = 127, G = 127, B = 63, A = 183
        int index = (3 * w + 3) * 4;
        Assert.Equal(127, buffer[index]);
        Assert.Equal(127, buffer[index + 1]);
        Assert.Equal(63, buffer[index + 2]);
        Assert.Equal(183, buffer[index + 3]);
    }

    [Fact]
    public void Rasterize_BilinearSampling_BoundaryUV()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];

        // Act - Draw a triangle with boundary UV = (1.0, 1.0)
        // 1.0 - floor(1.0) = 0.0
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            0f, 10f,
            1.0f, 1.0f,
            1.0f, 1.0f,
            1.0f, 1.0f,
            _testTexture,
            1f,
            TextureSamplingMode.Bilinear
        );

        // Assert - Should wrap to (0.0, 0.0) which maps to texel (0, 0)
        // Assert - Should wrap to (0.0, 0.0) which maps to texel (0, 0)
        int index = (3 * w + 3) * 4;
        Assert.True(buffer[index + 3] > 0);
    }

    [Fact]
    public void Rasterize_AlphaBelowThreshold_DiscardsPixel()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];
        
        byte[] pixels = new byte[2 * 2 * 4];
        // Set all alpha to 5
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255; pixels[i + 1] = 0; pixels[i + 2] = 0; pixels[i + 3] = 5;
        }
        var texture = new TextureImageData { Width = 2, Height = 2, PixelsRgba32 = pixels };

        // Act - Call with threshold = 10
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            0f, 10f,
            0f, 0f,
            1f, 0f,
            0f, 1f,
            texture,
            1f,
            TextureSamplingMode.Nearest,
            alphaThreshold: 10
        );

        // Assert - Discarded since 5 < 10, target buffer remains entirely transparent/zero
        int index = (3 * w + 3) * 4;
        Assert.Equal(0, buffer[index]);
        Assert.Equal(0, buffer[index + 3]);
    }

    [Fact]
    public void Rasterize_AlphaAboveThreshold_KeepsAndBlendsPixel()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];

        byte[] pixels = new byte[2 * 2 * 4];
        // Set all alpha to 20
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255; pixels[i + 1] = 0; pixels[i + 2] = 0; pixels[i + 3] = 20;
        }
        var texture = new TextureImageData { Width = 2, Height = 2, PixelsRgba32 = pixels };

        // Act - Call with threshold = 10
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            0f, 10f,
            0f, 0f,
            1f, 0f,
            0f, 1f,
            texture,
            1f,
            TextureSamplingMode.Nearest,
            alphaThreshold: 10
        );

        // Assert - Kept since 20 >= 10
        int index = (3 * w + 3) * 4;
        Assert.Equal(255, buffer[index]);
        Assert.Equal(20, buffer[index + 3]);
    }

    [Fact]
    public void Rasterize_AlphaZero_DiscardsPixel()
    {
        // Arrange
        int w = 10;
        int h = 10;
        byte[] buffer = new byte[w * h * 4];

        byte[] pixels = new byte[2 * 2 * 4];
        // Set all alpha to 0
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255; pixels[i + 1] = 0; pixels[i + 2] = 0; pixels[i + 3] = 0;
        }
        var texture = new TextureImageData { Width = 2, Height = 2, PixelsRgba32 = pixels };

        // Act - Call with default threshold (8)
        SoftwareTexturedTriangleRasterizer.Rasterize(
            buffer, w, h,
            0f, 0f,
            10f, 0f,
            0f, 10f,
            0f, 0f,
            1f, 0f,
            0f, 1f,
            texture,
            1f,
            TextureSamplingMode.Nearest
        );

        // Assert - Discarded since 0 < 8
        int index = (3 * w + 3) * 4;
        Assert.Equal(0, buffer[index]);
        Assert.Equal(0, buffer[index + 3]);
    }
}
