using System;
using System.Numerics;
using Xunit;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Tests;

public class CameraTransformCalculatorTests
{
    [Fact]
    public void Calculate_WithNullState_ReturnsDefaultTransform()
    {
        // Act
        var matrix = CameraTransformCalculator.Calculate(null);

        // Assert - verify it matches new PreviewCameraState() (Yaw 45, Pitch -30, Distance 5)
        var expectedMatrix = CameraTransformCalculator.Calculate(new PreviewCameraState());
        Assert.Equal(expectedMatrix, matrix);
    }

    [Fact]
    public void Calculate_WithYawOnly_RotationMatrixIsCorrect()
    {
        // Arrange
        // Yaw = 90 degrees around Y axis (in radians = PI/2)
        var state = new PreviewCameraState { Yaw = 90f, Pitch = 0f, Distance = 5f };

        // Act
        var matrix = CameraTransformCalculator.Calculate(state);

        // Assert - transforming a vector (1, 0, 0) should yield (0, 0, -1) with Y-rotation
        // (X = cos(90) = 0, Z = -sin(90) = -1)
        var testVector = new Vector3(1f, 0f, 0f);
        var transformed = Vector3.Transform(testVector, matrix);

        Assert.Equal(0f, transformed.X, 3);
        Assert.Equal(0f, transformed.Y, 3);
        Assert.Equal(-1f, transformed.Z, 3);
    }

    [Fact]
    public void Calculate_WithPitchOnly_RotationMatrixIsCorrect()
    {
        // Arrange
        // Pitch = 90 degrees around X axis (in radians = PI/2)
        var state = new PreviewCameraState { Yaw = 0f, Pitch = 90f, Distance = 5f };

        // Act
        var matrix = CameraTransformCalculator.Calculate(state);

        // Assert - transforming a vector (0, 1, 0) should yield (0, 0, 1) with X-rotation
        var testVector = new Vector3(0f, 1f, 0f);
        var transformed = Vector3.Transform(testVector, matrix);

        Assert.Equal(0f, transformed.X, 3);
        Assert.Equal(0f, transformed.Y, 3);
        Assert.Equal(1f, transformed.Z, 3);
    }

    [Fact]
    public void Calculate_WithDistanceZoom_ScalesCorrectly()
    {
        // Arrange
        // Zoom factor: 5.0 / Distance. For distance = 10, zoom = 0.5f.
        var state = new PreviewCameraState { Yaw = 0f, Pitch = 0f, Distance = 10f };

        // Act
        var matrix = CameraTransformCalculator.Calculate(state);

        // Assert
        var testVector = new Vector3(1f, 2f, 3f);
        var transformed = Vector3.Transform(testVector, matrix);

        Assert.Equal(0.5f, transformed.X, 3);
        Assert.Equal(1.0f, transformed.Y, 3);
        Assert.Equal(1.5f, transformed.Z, 3);
    }

    [Fact]
    public void Calculate_WithCloseDistanceZoom_ScalesCorrectly()
    {
        // Arrange
        // Zoom factor: 5.0 / Distance. For distance = 2.5, zoom = 2.0f.
        var state = new PreviewCameraState { Yaw = 0f, Pitch = 0f, Distance = 2.5f };

        // Act
        var matrix = CameraTransformCalculator.Calculate(state);

        // Assert
        var testVector = new Vector3(1f, 2f, 3f);
        var transformed = Vector3.Transform(testVector, matrix);

        Assert.Equal(2.0f, transformed.X, 3);
        Assert.Equal(4.0f, transformed.Y, 3);
        Assert.Equal(6.0f, transformed.Z, 3);
    }
}
