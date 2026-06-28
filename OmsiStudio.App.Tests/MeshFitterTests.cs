using System;
using System.Collections.Generic;
using Xunit;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Tests;

public class MeshFitterTests
{
    [Fact]
    public void FitToClipSpace_WithNullOrEmptyVertices_ReturnsEmptyArrayAndDefaultValues()
    {
        // Act (Null)
        var resultNull = MeshFitter.FitToClipSpace(null!);
        // Act (Empty)
        var resultEmpty = MeshFitter.FitToClipSpace(new List<O3dVertex>());

        // Assert
        Assert.Empty(resultNull.Vertices);
        Assert.Equal(1f, resultNull.Scale);
        Assert.Equal(0f, resultNull.OffsetX);
        Assert.Equal(0f, resultNull.OffsetY);
        Assert.Equal(0f, resultNull.OffsetZ);

        Assert.Empty(resultEmpty.Vertices);
        Assert.Equal(1f, resultEmpty.Scale);
        Assert.Equal(0f, resultEmpty.OffsetX);
        Assert.Equal(0f, resultEmpty.OffsetY);
        Assert.Equal(0f, resultEmpty.OffsetZ);
    }

    [Fact]
    public void FitToClipSpace_WithSingleVertex_CentersToZeroAndUsesUnitScale()
    {
        // Arrange
        var vertices = new List<O3dVertex>
        {
            new() { X = 10f, Y = 20f, Z = 30f }
        };

        // Act
        var (fitVertices, scale, offsetX, offsetY, offsetZ) = MeshFitter.FitToClipSpace(vertices);

        // Assert
        Assert.Equal(3, fitVertices.Length);
        Assert.Equal(0f, fitVertices[0]); // (10 - 10) * 1
        Assert.Equal(0f, fitVertices[1]); // (20 - 20) * 1
        Assert.Equal(0f, fitVertices[2]); // (30 - 30) * 1
        Assert.Equal(1f, scale);
        Assert.Equal(-10f, offsetX);
        Assert.Equal(-20f, offsetY);
        Assert.Equal(-30f, offsetZ);
    }

    [Fact]
    public void FitToClipSpace_WithRegularMesh_FitsWithinVisibleRange()
    {
        // Arrange
        var vertices = new List<O3dVertex>
        {
            new() { X = -5f, Y = 0f, Z = 1f },
            new() { X = 5f, Y = 10f, Z = 2f }
        };
        // center is (0, 5, 1.5). size is (10, 10, 1). maxSize is 10.
        // scale = 1.8f / 10f = 0.18f.

        // Act
        var (fitVertices, scale, offsetX, offsetY, offsetZ) = MeshFitter.FitToClipSpace(vertices);

        // Assert
        Assert.Equal(6, fitVertices.Length);
        Assert.Equal(0.18f, scale, 3);
        
        // V0: X = (-5 - 0)*0.18 = -0.9; Y = (0 - 5)*0.18 = -0.9; Z = (1 - 1.5)*0.18 = -0.09
        Assert.Equal(-0.9f, fitVertices[0], 3);
        Assert.Equal(-0.9f, fitVertices[1], 3);
        Assert.Equal(-0.09f, fitVertices[2], 3);

        // V1: X = (5 - 0)*0.18 = 0.9; Y = (10 - 5)*0.18 = 0.9; Z = (2 - 1.5)*0.18 = 0.09
        Assert.Equal(0.9f, fitVertices[3], 3);
        Assert.Equal(0.9f, fitVertices[4], 3);
        Assert.Equal(0.09f, fitVertices[5], 3);

        // All points fit within [-0.9, 0.9] range
        foreach (var val in fitVertices)
        {
            Assert.True(val >= -0.9001f && val <= 0.9001f);
        }
    }

    [Fact]
    public void FitToClipSpace_WithZeroSizeBounds_ReturnsCenteredAndDoesNotCrash()
    {
        // Arrange
        var vertices = new List<O3dVertex>
        {
            new() { X = 2f, Y = 2f, Z = 2f },
            new() { X = 2f, Y = 2f, Z = 2f }
        };

        // Act
        var (fitVertices, scale, offsetX, offsetY, offsetZ) = MeshFitter.FitToClipSpace(vertices);

        // Assert
        Assert.Equal(6, fitVertices.Length);
        Assert.Equal(1f, scale);
        Assert.Equal(0f, fitVertices[0]);
        Assert.Equal(0f, fitVertices[1]);
        Assert.Equal(0f, fitVertices[2]);
        Assert.Equal(0f, fitVertices[3]);
        Assert.Equal(0f, fitVertices[4]);
        Assert.Equal(0f, fitVertices[5]);
    }
}
