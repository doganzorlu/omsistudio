using System;
using Avalonia;
using Avalonia.Media;
using OmsiStudio.App.ViewModels;
using OmsiStudio.Core.Assets;
using Xunit;

namespace OmsiStudio.App.Tests;

public class MaterialDisplayItemTests : IDisposable
{
    private class DummyImage : IImage
    {
        public Size Size => new Size(16, 16);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
    }

    public MaterialDisplayItemTests()
    {
        // Clear factory before each test run
        MaterialDisplayItem.ThumbnailFactory = null;
    }

    public void Dispose()
    {
        // Clear factory after each test run
        MaterialDisplayItem.ThumbnailFactory = null;
    }

    [Fact]
    public void Constructor_WithBoundTexture_ExposesSizeAndStatus()
    {
        // Arrange
        var image = new TextureImageData
        {
            Width = 64,
            Height = 64,
            PixelsRgba32 = new byte[64 * 64 * 4]
        };
        var binding = new MaterialTextureBinding
        {
            MaterialIndex = 0,
            MaterialName = "Material 0",
            TextureReference = "tex.png",
            Status = TextureBindingStatus.Bound,
            Image = image
        };

        // Act
        var displayItem = new MaterialDisplayItem("Material 0", "tex.png", Colors.Red, "Not bound", binding);

        // Assert - Size and status must always be exposed regardless of headless renderer environment
        Assert.Equal("Material 0", displayItem.MaterialName);
        Assert.Equal("tex.png", displayItem.TextureReference);
        Assert.Equal("Bound", displayItem.BindingStatus);
        Assert.Equal("64x64", displayItem.ImageSizeText);
        Assert.False(displayItem.HasDiagnostics);
    }

    [Fact]
    public void Constructor_WithThumbnailFactory_ExposesThumbnail()
    {
        // Arrange
        var dummyImage = new DummyImage();
        MaterialDisplayItem.ThumbnailFactory = img => dummyImage;

        var image = new TextureImageData
        {
            Width = 16,
            Height = 16,
            PixelsRgba32 = new byte[16 * 16 * 4]
        };
        var binding = new MaterialTextureBinding
        {
            MaterialIndex = 0,
            MaterialName = "Material 0",
            TextureReference = "tex.png",
            Status = TextureBindingStatus.Bound,
            Image = image
        };

        // Act
        var displayItem = new MaterialDisplayItem("Material 0", "tex.png", Colors.Red, "Not bound", binding);

        // Assert - Thumbnail factory successfully produces the dummy image
        Assert.True(displayItem.HasTextureThumbnail);
        Assert.Same(dummyImage, displayItem.TextureThumbnail);
    }

    [Fact]
    public void Constructor_WithNullOrThrowingFactory_GracefullyHandlesFailure()
    {
        // Arrange - Setup factory that throws exception
        MaterialDisplayItem.ThumbnailFactory = img => throw new InvalidOperationException("Headless error");

        var image = new TextureImageData
        {
            Width = 16,
            Height = 16,
            PixelsRgba32 = new byte[16 * 16 * 4]
        };
        var binding = new MaterialTextureBinding
        {
            MaterialIndex = 0,
            MaterialName = "Material 0",
            TextureReference = "tex.png",
            Status = TextureBindingStatus.Bound,
            Image = image
        };

        // Act & Assert (Should not throw and should leave thumbnail null)
        var displayItem = new MaterialDisplayItem("Material 0", "tex.png", Colors.Red, "Not bound", binding);
        Assert.False(displayItem.HasTextureThumbnail);
        Assert.Null(displayItem.TextureThumbnail);
    }

    [Fact]
    public void Constructor_WithMissingTexture_ExposesMissingStatusAndDiagnostics()
    {
        // Arrange
        var binding = new MaterialTextureBinding
        {
            MaterialIndex = 1,
            MaterialName = "Material 1",
            TextureReference = "missing.png",
            Status = TextureBindingStatus.Missing,
            Diagnostics = new[]
            {
                new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Warning, Message = "Doku dosyası bulunamadı" }
            }
        };

        // Act
        var displayItem = new MaterialDisplayItem("Material 1", "missing.png", Colors.Blue, "Not bound", binding);

        // Assert
        Assert.Equal("Missing", displayItem.BindingStatus);
        Assert.False(displayItem.HasTextureThumbnail);
        Assert.True(displayItem.HasDiagnostics);
        Assert.Equal("Doku dosyası bulunamadı", displayItem.DiagnosticsText);
    }

    [Fact]
    public void Constructor_WithNullBinding_DoesNotCrash()
    {
        // Act
        var displayItem = new MaterialDisplayItem("Material Plain", "none", Colors.Green, "Not bound", null);

        // Assert
        Assert.Equal("Material Plain", displayItem.MaterialName);
        Assert.Equal("none", displayItem.TextureReference);
        Assert.Equal("Not bound", displayItem.BindingStatus);
        Assert.False(displayItem.HasTextureThumbnail);
        Assert.False(displayItem.HasDiagnostics);
    }
}
