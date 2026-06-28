using System;
using System.Collections.Generic;
using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.Core.Tests;

/// <summary>
/// Unit tests verifying coordinate processing, min/max extraction, and default behaviors of the MeshBoundsCalculator.
/// </summary>
public class MeshBoundsCalculatorTests
{
    [Fact]
    public void CalculateBounds_WithNullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var calculator = new MeshBoundsCalculator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => calculator.CalculateBounds(null!));
    }

    [Fact]
    public void CalculateBounds_WithEmptyVertices_ReturnsZeroBounds()
    {
        // Arrange
        var calculator = new MeshBoundsCalculator();
        var meshData = new O3dMeshData { Vertices = new List<O3dVertex>() };

        // Act
        var bounds = calculator.CalculateBounds(meshData);

        // Assert
        Assert.NotNull(bounds);
        Assert.Equal(0f, bounds.Min.X);
        Assert.Equal(0f, bounds.Min.Y);
        Assert.Equal(0f, bounds.Min.Z);
        Assert.Equal(0f, bounds.Max.X);
        Assert.Equal(0f, bounds.Max.Y);
        Assert.Equal(0f, bounds.Max.Z);
        Assert.Equal(0f, bounds.Center.X);
        Assert.Equal(0f, bounds.Center.Y);
        Assert.Equal(0f, bounds.Center.Z);
        Assert.Equal(0f, bounds.Size.X);
        Assert.Equal(0f, bounds.Size.Y);
        Assert.Equal(0f, bounds.Size.Z);
    }

    [Fact]
    public void CalculateBounds_WithSingleVertex_ReturnsEqualMinMaxAndZeroSize()
    {
        // Arrange
        var calculator = new MeshBoundsCalculator();
        var vertices = new List<O3dVertex>
        {
            new() { X = 1.5f, Y = -2.5f, Z = 3.0f }
        };
        var meshData = new O3dMeshData { Vertices = vertices };

        // Act
        var bounds = calculator.CalculateBounds(meshData);

        // Assert
        Assert.NotNull(bounds);
        Assert.Equal(1.5f, bounds.Min.X);
        Assert.Equal(-2.5f, bounds.Min.Y);
        Assert.Equal(3.0f, bounds.Min.Z);

        Assert.Equal(1.5f, bounds.Max.X);
        Assert.Equal(-2.5f, bounds.Max.Y);
        Assert.Equal(3.0f, bounds.Max.Z);

        Assert.Equal(1.5f, bounds.Center.X);
        Assert.Equal(-2.5f, bounds.Center.Y);
        Assert.Equal(3.0f, bounds.Center.Z);

        Assert.Equal(0f, bounds.Size.X);
        Assert.Equal(0f, bounds.Size.Y);
        Assert.Equal(0f, bounds.Size.Z);
    }

    [Fact]
    public void CalculateBounds_WithAsymmetricAndNegativeVertices_CalculatesCorrectBounds()
    {
        // Arrange
        var calculator = new MeshBoundsCalculator();
        var vertices = new List<O3dVertex>
        {
            new() { X = -10f, Y = 5f, Z = -2f },
            new() { X = 20f, Y = -15f, Z = 8f },
            new() { X = 5f, Y = 0f, Z = 4f }
        };
        var meshData = new O3dMeshData { Vertices = vertices };

        // Act
        var bounds = calculator.CalculateBounds(meshData);

        // Assert
        Assert.Equal(-10f, bounds.Min.X);
        Assert.Equal(-15f, bounds.Min.Y);
        Assert.Equal(-2f, bounds.Min.Z);

        Assert.Equal(20f, bounds.Max.X);
        Assert.Equal(5f, bounds.Max.Y);
        Assert.Equal(8f, bounds.Max.Z);

        Assert.Equal(5f, bounds.Center.X);
        Assert.Equal(-5f, bounds.Center.Y);
        Assert.Equal(3f, bounds.Center.Z);

        Assert.Equal(30f, bounds.Size.X);
        Assert.Equal(20f, bounds.Size.Y);
        Assert.Equal(10f, bounds.Size.Z);
    }
}
