using System;
using System.Collections.Generic;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// A helper class to fit mesh coordinates into OpenGL clip-space boundaries.
/// </summary>
public static class MeshFitter
{
    /// <summary>
    /// Computes bounding box scale and offsets to center and fit vertices within clip-space [-0.9, 0.9] range.
    /// Handles empty, single-point, and zero-size bounds safely.
    /// </summary>
    /// <param name="vertices">The source vertices of the mesh.</param>
    /// <returns>A tuple containing the normalized vertices data array, scale factor, and translations.</returns>
    public static (float[] Vertices, float Scale, float OffsetX, float OffsetY, float OffsetZ) FitToClipSpace(IReadOnlyList<O3dVertex> vertices)
    {
        if (vertices == null || vertices.Count == 0)
        {
            return (Array.Empty<float>(), 1f, 0f, 0f, 0f);
        }

        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        foreach (var v in vertices)
        {
            if (v.X < minX) minX = v.X;
            if (v.X > maxX) maxX = v.X;
            if (v.Y < minY) minY = v.Y;
            if (v.Y > maxY) maxY = v.Y;
            if (v.Z < minZ) minZ = v.Z;
            if (v.Z > maxZ) maxZ = v.Z;
        }

        // Center coordinates
        float centerX = (minX + maxX) / 2f;
        float centerY = (minY + maxY) / 2f;
        float centerZ = (minZ + maxZ) / 2f;

        // Dimensions
        float sizeX = maxX - minX;
        float sizeY = maxY - minY;
        float sizeZ = maxZ - minZ;

        float maxSize = Math.Max(sizeX, Math.Max(sizeY, sizeZ));

        // If bounds are empty/zero size or single-point, or maxSize is extremely close to zero
        float scale = 1f;
        if (maxSize > 1e-6f)
        {
            // Normalize to fit within [-0.9, 0.9] to leave some padding at the viewport edges
            scale = 1.8f / maxSize;
        }

        float[] fitVertices = new float[vertices.Count * 3];
        for (int i = 0; i < vertices.Count; i++)
        {
            fitVertices[i * 3] = (vertices[i].X - centerX) * scale;
            fitVertices[i * 3 + 1] = (vertices[i].Y - centerY) * scale;
            fitVertices[i * 3 + 2] = (vertices[i].Z - centerZ) * scale;
        }

        return (fitVertices, scale, -centerX * scale, -centerY * scale, -centerZ * scale);
    }
}
