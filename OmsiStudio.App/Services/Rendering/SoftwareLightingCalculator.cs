using System;
using System.Numerics;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// Computes lighting intensity factors using flat ambient and directional light models.
/// </summary>
public static class SoftwareLightingCalculator
{
    /// <summary>
    /// Base ambient light intensity.
    /// </summary>
    public const float AmbientIntensity = 0.45f;

    /// <summary>
    /// Directional light contribution multiplier.
    /// </summary>
    public const float DirectionalIntensity = 0.55f;

    /// <summary>
    /// Minimum allowed final intensity value.
    /// </summary>
    public const float MinIntensity = 0.35f;

    /// <summary>
    /// Maximum allowed final intensity value.
    /// </summary>
    public const float MaxIntensity = 1.15f;

    /// <summary>
    /// Computes the lighting intensity factor based on flat normal shading.
    /// </summary>
    /// <param name="normal">The face normal vector.</param>
    /// <param name="viewDir">The normalized view/camera direction vector.</param>
    /// <param name="lightDir">The normalized directional light direction vector.</param>
    /// <returns>A clamped lighting intensity multiplier.</returns>
    public static float ComputeIntensity(Vector3 normal, Vector3 viewDir, Vector3 lightDir)
    {
        float len = normal.Length();
        if (len < 1e-6f)
        {
            // Degenerate/zero normal fallback
            return AmbientIntensity;
        }

        Vector3 n = normal / len;

        // Ensure light direction is normalized
        Vector3 l = lightDir;
        float lLen = l.Length();
        if (lLen > 1e-6f)
        {
            l /= lLen;
        }

        // Calculate diffuse contribution: dot product of normal and light direction
        float dot = Vector3.Dot(n, l);

        // Diffuse contribution
        float diffuse = Math.Max(0f, dot);

        float intensity = AmbientIntensity + DirectionalIntensity * diffuse;

        // Clamp final intensity to [MinIntensity, MaxIntensity]
        return Math.Clamp(intensity, MinIntensity, MaxIntensity);
    }
}
