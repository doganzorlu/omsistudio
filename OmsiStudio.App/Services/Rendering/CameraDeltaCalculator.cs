using System;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// A helper class to calculate camera property changes from mouse deltas.
/// </summary>
public static class CameraDeltaCalculator
{
    /// <summary>
    /// Computes the new camera yaw given a drag delta and sensitivity.
    /// </summary>
    public static float CalculateYaw(float currentYaw, double deltaX, float sensitivity)
    {
        // Wrap around to keep within [0, 360) or standard degrees
        float newYaw = (currentYaw + (float)deltaX * sensitivity) % 360f;
        if (newYaw < 0f)
        {
            newYaw += 360f;
        }
        return newYaw;
    }

    /// <summary>
    /// Computes the new camera pitch given a drag delta, sensitivity, and clamping.
    /// </summary>
    public static float CalculatePitch(float currentPitch, double deltaY, float sensitivity)
    {
        float newPitch = currentPitch - (float)deltaY * sensitivity;
        return Math.Clamp(newPitch, -89f, 89f);
    }

    /// <summary>
    /// Computes the new camera distance given wheel delta, sensitivity, and clamping.
    /// </summary>
    public static float CalculateDistance(float currentDistance, double deltaWheel, float sensitivity)
    {
        float newDistance = currentDistance - (float)deltaWheel * sensitivity;
        return Math.Clamp(newDistance, 0.5f, 50f);
    }
}
