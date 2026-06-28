using System.Numerics;
using OmsiStudio.App.Services.Rendering;
using Xunit;

namespace OmsiStudio.App.Tests;

public class SoftwareLightingCalculatorTests
{
    [Fact]
    public void ComputeIntensity_LightFacingNormal_ProducesHighIntensity()
    {
        // Arrange
        // Normal directly aligned with light direction
        var normal = new Vector3(0.3f, 0.5f, 0.824f);
        var viewDir = new Vector3(0f, 0f, 1f);
        var lightDir = new Vector3(0.3f, 0.5f, 0.824f);

        // Act
        float intensity = SoftwareLightingCalculator.ComputeIntensity(normal, viewDir, lightDir);

        // Assert: Dot product is ~1.0, so intensity is 0.45 + 0.55 * 1 = 1.0
        Assert.InRange(intensity, 0.95f, 1.05f);
    }

    [Fact]
    public void ComputeIntensity_SideNormal_ProducesAmbientOrMediumIntensity()
    {
        // Arrange
        // Normal orthogonal to light direction
        var lightDir = new Vector3(1f, 0f, 0f);
        var normal = new Vector3(0f, 1f, 0f);
        var viewDir = new Vector3(0f, 0f, 1f);

        // Act
        float intensity = SoftwareLightingCalculator.ComputeIntensity(normal, viewDir, lightDir);

        // Assert: Dot product is 0.0, so intensity is 0.45 + 0.55 * 0 = 0.45
        Assert.Equal(0.45f, intensity);
    }

    [Fact]
    public void ComputeIntensity_BackFacingNormal_StaysAboveMinAndDoesNotGoZero()
    {
        // Arrange
        // Normal facing directly opposite to light direction
        var lightDir = new Vector3(0f, 0f, 1f);
        var normal = new Vector3(0f, 0f, -1f);
        var viewDir = new Vector3(0f, 0f, 1f);

        // Act
        float intensity = SoftwareLightingCalculator.ComputeIntensity(normal, viewDir, lightDir);

        // Assert: Dot product is -1.0, so diffuse term is 0.0, intensity is 0.45.
        // Clamped to [0.35, 1.15] -> remains 0.45, which is > MinIntensity (0.35)
        Assert.Equal(0.45f, intensity);
    }

    [Fact]
    public void ComputeIntensity_DegenerateNormal_ReturnsDeterministicFallback()
    {
        // Arrange
        var normal = Vector3.Zero;
        var viewDir = new Vector3(0f, 0f, 1f);
        var lightDir = new Vector3(1f, 0f, 0f);

        // Act
        float intensity = SoftwareLightingCalculator.ComputeIntensity(normal, viewDir, lightDir);

        // Assert: Should return AmbientIntensity (0.45) as fallback
        Assert.Equal(0.45f, intensity);
    }
}
