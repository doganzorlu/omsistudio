using System;
using System.Numerics;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// Computes the camera transformation matrix based on orbit yaw, pitch, and distance.
/// </summary>
public static class CameraTransformCalculator
{
    /// <summary>
    /// Calculates the 4x4 row-major transformation matrix for the given camera state.
    /// </summary>
    /// <param name="cameraState">The camera state parameters.</param>
    /// <returns>A row-major <see cref="Matrix4x4"/> transformation matrix.</returns>
    public static Matrix4x4 Calculate(PreviewCameraState? cameraState)
    {
        var camera = cameraState ?? new PreviewCameraState();

        // Convert angles to radians
        float yawRad = camera.Yaw * (float)Math.PI / 180f;
        float pitchRad = camera.Pitch * (float)Math.PI / 180f;

        // Apply rotations (pitch around X axis, yaw around Y axis)
        var rotation = Matrix4x4.CreateRotationY(yawRad) * Matrix4x4.CreateRotationX(pitchRad);
        
        // Zoom scaling factor: 5.0 is the default distance zoom level
        float zoom = 5.0f / Math.Max(0.1f, camera.Distance);
        var scale = Matrix4x4.CreateScale(zoom);

        return rotation * scale;
    }
}
