using System.Collections.Generic;
using Avalonia.Media;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.Core.Assets;
using Xunit;

namespace OmsiStudio.App.Tests;

public class MaterialColorSelectorTests
{
    [Fact]
    public void GetMaterialColor_NullOrInvalidParameters_ReturnsDefaultSlateColor()
    {
        // Arrange
        var mesh = new O3dMeshData
        {
            MaterialSlots = new List<O3dMaterialSlot>
            {
                new O3dMaterialSlot { TextureReference = "texture1.bmp" }
            }
        };
        var expectedDefaultColor = Color.FromRgb(46, 48, 62);

        // Act & Assert
        Assert.Equal(expectedDefaultColor, MaterialColorSelector.GetMaterialColor((O3dMeshData?)null, 0));
        Assert.Equal(expectedDefaultColor, MaterialColorSelector.GetMaterialColor(mesh, null));
        Assert.Equal(expectedDefaultColor, MaterialColorSelector.GetMaterialColor(mesh, -1));
        Assert.Equal(expectedDefaultColor, MaterialColorSelector.GetMaterialColor(mesh, 1)); // Out of bounds index
        Assert.Equal(expectedDefaultColor, MaterialColorSelector.GetMaterialColor(new O3dMeshData { MaterialSlots = null! }, 0));
    }

    [Fact]
    public void GetMaterialColor_ValidIndex_ReturnsDeterministicColor()
    {
        // Arrange
        var mesh = new O3dMeshData
        {
            MaterialSlots = new List<O3dMaterialSlot>
            {
                new O3dMaterialSlot { TextureReference = "texture0.bmp" },
                new O3dMaterialSlot { TextureReference = "texture1.bmp" }
            }
        };

        // Act
        var color0_first = MaterialColorSelector.GetMaterialColor(mesh, 0);
        var color0_second = MaterialColorSelector.GetMaterialColor(mesh, 0);
        var color1 = MaterialColorSelector.GetMaterialColor(mesh, 1);

        // Assert
        Assert.NotEqual(Color.FromRgb(46, 48, 62), color0_first); // Should not be fallback color
        Assert.Equal(color0_first, color0_second); // Should be deterministic
        Assert.NotEqual(color0_first, color1); // Different slots should have different colors
    }

    [Fact]
    public void GetShadedColor_DegenerateTriangle_ReturnsFallbackWithoutNaN()
    {
        // Arrange
        var baseColor = Color.FromRgb(100, 150, 200);
        var zeroNormal = System.Numerics.Vector3.Zero;

        // Act
        var resultColor = MaterialColorSelector.GetShadedColor(baseColor, zeroNormal);

        // Assert
        // intensity should fall back to 0, factor = 0.4f.
        Assert.Equal((byte)(baseColor.R * 0.4f), resultColor.R);
        Assert.Equal((byte)(baseColor.G * 0.4f), resultColor.G);
        Assert.Equal((byte)(baseColor.B * 0.4f), resultColor.B);
    }

    [Fact]
    public void GetShadedColor_ValidMaterialWithDegenerateNormal_ReturnsDeterministicFallbackColor()
    {
        // Arrange
        var mesh = new O3dMeshData
        {
            MaterialSlots = new List<O3dMaterialSlot>
            {
                new O3dMaterialSlot { TextureReference = "tex.png" }
            }
        };
        var baseColor = MaterialColorSelector.GetMaterialColor(mesh, 0);
        var degenerateNormal = new System.Numerics.Vector3(1e-10f, 1e-10f, 1e-10f); // Length < 1e-6

        // Act
        var resultColor = MaterialColorSelector.GetShadedColor(baseColor, degenerateNormal);

        // Assert
        Assert.Equal((byte)(baseColor.R * 0.4f), resultColor.R);
        Assert.Equal((byte)(baseColor.G * 0.4f), resultColor.G);
        Assert.Equal((byte)(baseColor.B * 0.4f), resultColor.B);
    }

    [Fact]
    public void GetMaterialColor_SameTexturePath_ReturnsSameColor()
    {
        // Arrange
        var mesh1 = new O3dMeshData { MaterialSlots = new List<O3dMaterialSlot> { new O3dMaterialSlot { TextureReference = "same.png" } } };
        var mesh2 = new O3dMeshData { MaterialSlots = new List<O3dMaterialSlot> { new O3dMaterialSlot { TextureReference = "same.png" } } };

        // Act
        var color1 = MaterialColorSelector.GetMaterialColor(mesh1, 0);
        var color2 = MaterialColorSelector.GetMaterialColor(mesh2, 0);

        // Assert
        Assert.Equal(color1, color2);
    }

    [Fact]
    public void GetMaterialColor_DifferentTexturePaths_ReturnsDifferentColors()
    {
        // Arrange
        var mesh = new O3dMeshData
        {
            MaterialSlots = new List<O3dMaterialSlot>
            {
                new O3dMaterialSlot { TextureReference = "diff1.png" },
                new O3dMaterialSlot { TextureReference = "diff2.png" }
            }
        };

        // Act
        var color1 = MaterialColorSelector.GetMaterialColor(mesh, 0);
        var color2 = MaterialColorSelector.GetMaterialColor(mesh, 1);

        // Assert
        Assert.NotEqual(color1, color2);
    }

    [Fact]
    public void GetMaterialColor_NullOrEmptyTexturePath_FallsBackToMaterialIndexColor()
    {
        // Arrange
        var meshWithNullTex = new O3dMeshData { MaterialSlots = new List<O3dMaterialSlot> { new O3dMaterialSlot { TextureReference = null } } };
        var meshWithEmptyTex = new O3dMeshData { MaterialSlots = new List<O3dMaterialSlot> { new O3dMaterialSlot { TextureReference = "" } } };

        // Act
        var colorNull = MaterialColorSelector.GetMaterialColor(meshWithNullTex, 0);
        var colorEmpty = MaterialColorSelector.GetMaterialColor(meshWithEmptyTex, 0);

        // Assert
        // They should fall back to the index-based color. Since index is 0 in both, they should be equal to each other.
        Assert.Equal(colorNull, colorEmpty);
        Assert.NotEqual(Color.FromRgb(46, 48, 62), colorNull); // Should not be fallback slate color
    }
}
