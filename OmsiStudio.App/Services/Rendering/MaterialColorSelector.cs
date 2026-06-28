using System;
using System.Numerics;
using Avalonia.Media;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// Selects deterministic colors for mesh triangles based on material slots.
/// </summary>
public static class MaterialColorSelector
{
    /// <summary>
    /// Gets the base color for a material slot index, falling back if null or invalid.
    /// </summary>
    public static Color GetMaterialColor(O3dMeshData? mesh, int? materialSlotIndex)
    {
        if (mesh == null || materialSlotIndex == null || materialSlotIndex < 0 || mesh.MaterialSlots == null || materialSlotIndex.Value >= mesh.MaterialSlots.Count)
        {
            // Fallback default slate color: #2e303e (RGB: 46, 48, 62)
            return Color.FromRgb(46, 48, 62);
        }

        var slot = mesh.MaterialSlots[materialSlotIndex.Value];
        if (slot != null && !string.IsNullOrEmpty(slot.TextureReference))
        {
            int stringHash = GetDeterministicStringHash(slot.TextureReference);
            float textureHue = Math.Abs(stringHash % 360);
            return ColorFromHsl(textureHue, 0.4f, 0.35f);
        }

        int idx = materialSlotIndex.Value;
        int hash = (idx * 16777619) ^ 8191;
        float hue = Math.Abs(hash % 360);
        return ColorFromHsl(hue, 0.4f, 0.35f);
    }

    private static int GetDeterministicStringHash(string value)
    {
        int hash = 5381;
        foreach (char c in value)
        {
            hash = ((hash << 5) + hash) ^ c;
        }
        return hash;
    }

    /// <summary>
    /// Computes the cross product of the view-space triangle edges to get the normal vector.
    /// </summary>
    public static Vector3 CalculateViewSpaceNormal(Vector3 v0_view, Vector3 v1_view, Vector3 v2_view)
    {
        return Vector3.Cross(v1_view - v0_view, v2_view - v0_view);
    }

    /// <summary>
    /// Calculates the final color after applying flat view-space normal shading factor.
    /// Safely handles degenerate normals to prevent division-by-zero or NaNs.
    /// </summary>
    public static Color GetShadedColor(Color baseColor, Vector3 viewSpaceNormal)
    {
        float intensity = 0f;
        float len = viewSpaceNormal.Length();
        if (len > 1e-6f)
        {
            Vector3 normalized = viewSpaceNormal / len;
            intensity = Math.Abs(normalized.Z);
        }

        float factor = 0.4f + 0.6f * intensity;
        byte r = (byte)(baseColor.R * factor);
        byte g = (byte)(baseColor.G * factor);
        byte b = (byte)(baseColor.B * factor);
        return Color.FromRgb(r, g, b);
    }

    private static Color ColorFromHsl(float h, float s, float l)
    {
        float c = (1f - Math.Abs(2f * l - 1f)) * s;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = l - c / 2f;

        float r1 = 0, g1 = 0, b1 = 0;
        if (h >= 0 && h < 60) { r1 = c; g1 = x; }
        else if (h >= 60 && h < 120) { r1 = x; g1 = c; }
        else if (h >= 120 && h < 180) { g1 = c; b1 = x; }
        else if (h >= 180 && h < 240) { g1 = x; b1 = c; }
        else if (h >= 240 && h < 300) { r1 = x; b1 = c; }
        else if (h >= 300 && h < 360) { r1 = c; b1 = x; }

        byte r = (byte)((r1 + m) * 255);
        byte g = (byte)((g1 + m) * 255);
        byte b = (byte)((b1 + m) * 255);
        return Color.FromRgb(r, g, b);
    }
}
