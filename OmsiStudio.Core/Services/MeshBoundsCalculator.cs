using System;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Services;

/// <summary>
/// Implements concrete IMeshBoundsCalculator to compute mesh bounds using raw vertex coordinates.
/// </summary>
public class MeshBoundsCalculator : IMeshBoundsCalculator
{
    /// <summary>
    /// Calculates the axis-aligned bounding box (AABB) bounds of a mesh.
    /// </summary>
    public MeshBounds CalculateBounds(O3dMeshData meshData)
    {
        if (meshData == null)
        {
            throw new ArgumentNullException(nameof(meshData));
        }

        if (meshData.Vertices == null || meshData.Vertices.Count == 0)
        {
            return new MeshBounds
            {
                Min = new PreviewVector3D { X = 0f, Y = 0f, Z = 0f },
                Max = new PreviewVector3D { X = 0f, Y = 0f, Z = 0f },
                Center = new PreviewVector3D { X = 0f, Y = 0f, Z = 0f },
                Size = new PreviewVector3D { X = 0f, Y = 0f, Z = 0f }
            };
        }

        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        foreach (var vertex in meshData.Vertices)
        {
            if (vertex.X < minX) minX = vertex.X;
            if (vertex.X > maxX) maxX = vertex.X;

            if (vertex.Y < minY) minY = vertex.Y;
            if (vertex.Y > maxY) maxY = vertex.Y;

            if (vertex.Z < minZ) minZ = vertex.Z;
            if (vertex.Z > maxZ) maxZ = vertex.Z;
        }

        var minVec = new PreviewVector3D { X = minX, Y = minY, Z = minZ };
        var maxVec = new PreviewVector3D { X = maxX, Y = maxY, Z = maxZ };

        var centerVec = new PreviewVector3D
        {
            X = (minX + maxX) / 2f,
            Y = (minY + maxY) / 2f,
            Z = (minZ + maxZ) / 2f
        };

        var sizeVec = new PreviewVector3D
        {
            X = maxX - minX,
            Y = maxY - minY,
            Z = maxZ - minZ
        };

        return new MeshBounds
        {
            Min = minVec,
            Max = maxVec,
            Center = centerVec,
            Size = sizeVec
        };
    }
}
