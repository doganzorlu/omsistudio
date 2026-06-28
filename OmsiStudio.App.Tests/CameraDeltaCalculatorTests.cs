using Xunit;
using OmsiStudio.App.Services.Rendering;

namespace OmsiStudio.App.Tests;

public class CameraDeltaCalculatorTests
{
    [Theory]
    [InlineData(45f, 10, 0.5f, 50f)]
    [InlineData(45f, -10, 0.5f, 40f)]
    [InlineData(350f, 30, 0.5f, 5f)] // Wrap around positive
    [InlineData(10f, -30, 0.5f, 355f)] // Wrap around negative
    public void CalculateYaw_ShouldApplyDeltaAndWrapCorrectly(float currentYaw, double deltaX, float sensitivity, float expectedYaw)
    {
        var actual = CameraDeltaCalculator.CalculateYaw(currentYaw, deltaX, sensitivity);
        Assert.Equal(expectedYaw, actual, 3);
    }

    [Theory]
    [InlineData(0f, 10, 0.5f, -5f)]
    [InlineData(0f, -10, 0.5f, 5f)]
    [InlineData(80f, -30, 0.5f, 89f)] // Clamp upper bound
    [InlineData(-80f, 30, 0.5f, -89f)] // Clamp lower bound
    public void CalculatePitch_ShouldApplyDeltaAndClampCorrectly(float currentPitch, double deltaY, float sensitivity, float expectedPitch)
    {
        var actual = CameraDeltaCalculator.CalculatePitch(currentPitch, deltaY, sensitivity);
        Assert.Equal(expectedPitch, actual, 3);
    }

    [Theory]
    [InlineData(5f, 1, 1.0f, 4f)] // Scroll up (zoom in, decrease distance)
    [InlineData(5f, -1, 1.0f, 6f)] // Scroll down (zoom out, increase distance)
    [InlineData(1f, 2, 1.0f, 0.5f)] // Clamp lower bound
    [InlineData(49f, -2, 1.0f, 50f)] // Clamp upper bound
    public void CalculateDistance_ShouldApplyDeltaAndClampCorrectly(float currentDistance, double deltaWheel, float sensitivity, float expectedDistance)
    {
        var actual = CameraDeltaCalculator.CalculateDistance(currentDistance, deltaWheel, sensitivity);
        Assert.Equal(expectedDistance, actual, 3);
    }
}
