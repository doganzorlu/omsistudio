using System;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.App.Services.Rendering;

/// <summary>
/// A CPU-side textured triangle rasterizer core.
/// </summary>
public static class SoftwareTexturedTriangleRasterizer
{
    /// <summary>
    /// Rasterizes a textured triangle onto the target RGBA buffer.
    /// </summary>
    /// <param name="targetBuffer">The destination RGBA byte array (width * height * 4).</param>
    /// <param name="targetWidth">The width of the target buffer.</param>
    /// <param name="targetHeight">The height of the target buffer.</param>
    /// <param name="x0">X coordinate of the first vertex.</param>
    /// <param name="y0">Y coordinate of the first vertex.</param>
    /// <param name="x1">X coordinate of the second vertex.</param>
    /// <param name="y1">Y coordinate of the second vertex.</param>
    /// <param name="x2">X coordinate of the third vertex.</param>
    /// <param name="y2">Y coordinate of the third vertex.</param>
    /// <param name="u0">U coordinate of the first vertex.</param>
    /// <param name="v0">V coordinate of the first vertex.</param>
    /// <param name="u1">U coordinate of the second vertex.</param>
    /// <param name="v1">V coordinate of the second vertex.</param>
    /// <param name="u2">U coordinate of the third vertex.</param>
    /// <param name="v2">V coordinate of the third vertex.</param>
    /// <param name="texture">The texture source image.</param>
    /// <param name="intensity">The shading intensity multiplier (applied to RGB).</param>
    public static void Rasterize(
        byte[] targetBuffer,
        int targetWidth,
        int targetHeight,
        float x0, float y0,
        float x1, float y1,
        float x2, float y2,
        float u0, float v0,
        float u1, float v1,
        float u2, float v2,
        TextureImageData texture,
        float intensity = 1f,
        TextureSamplingMode samplingMode = TextureSamplingMode.Bilinear)
    {
        // 1. Guard against invalid inputs
        if (targetBuffer == null || targetWidth <= 0 || targetHeight <= 0)
        {
            return;
        }

        if (texture == null || texture.PixelsRgba32 == null || texture.Width <= 0 || texture.Height <= 0)
        {
            return;
        }

        long expectedTargetBytes;
        long expectedTextureBytes;
        try
        {
            checked
            {
                expectedTargetBytes = (long)targetWidth * targetHeight * 4;
                expectedTextureBytes = (long)texture.Width * texture.Height * 4;
            }
        }
        catch (OverflowException)
        {
            return;
        }

        if (targetBuffer.Length < expectedTargetBytes)
        {
            return;
        }

        if (texture.PixelsRgba32.Length < expectedTextureBytes)
        {
            return;
        }

        // 2. Degenerate triangle check
        // d is double the area of the triangle
        float d = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
        if (Math.Abs(d) < 1e-6f)
        {
            return;
        }

        // 3. Bounding box computation with clipping to viewport
        float minX = Math.Min(x0, Math.Min(x1, x2));
        float maxX = Math.Max(x0, Math.Max(x1, x2));
        float minY = Math.Min(y0, Math.Min(y1, y2));
        float maxY = Math.Max(y0, Math.Max(y1, y2));

        int bboxMinX = Math.Max(0, (int)Math.Floor(minX));
        int bboxMaxX = Math.Min(targetWidth - 1, (int)Math.Ceiling(maxX));
        int bboxMinY = Math.Max(0, (int)Math.Floor(minY));
        int bboxMaxY = Math.Min(targetHeight - 1, (int)Math.Ceiling(maxY));

        // Skip if bounding box is entirely out of bounds
        if (bboxMinX > bboxMaxX || bboxMinY > bboxMaxY)
        {
            return;
        }

        int texWidth = texture.Width;
        int texHeight = texture.Height;
        byte[] texPixels = texture.PixelsRgba32;

        // Helper to safely wrap texel coordinates (Modulo division)
        int wrapX(int val, int limit)
        {
            int rVal = val % limit;
            return rVal < 0 ? rVal + limit : rVal;
        }

        // 4. Rasterization loop
        for (int py = bboxMinY; py <= bboxMaxY; py++)
        {
            for (int px = bboxMinX; px <= bboxMaxX; px++)
            {
                // Pixel center
                float cx = px + 0.5f;
                float cy = py + 0.5f;

                // Compute barycentric coordinates
                float w1 = ((cx - x0) * (y2 - y0) - (x2 - x0) * (cy - y0)) / d;
                float w2 = ((x1 - x0) * (cy - y0) - (cx - x0) * (y1 - y0)) / d;
                float w0 = 1f - w1 - w2;

                // Check if the point lies inside the triangle (with tiny precision tolerance)
                if (w0 >= -1e-5f && w1 >= -1e-5f && w2 >= -1e-5f)
                {
                    // Clamp weights to [0, 1] for interpolation safety
                    float weight0 = Math.Clamp(w0, 0f, 1f);
                    float weight1 = Math.Clamp(w1, 0f, 1f);
                    float weight2 = Math.Clamp(w2, 0f, 1f);

                    // Re-normalize weights so they sum exactly to 1.0
                    float totalWeight = weight0 + weight1 + weight2;
                    if (totalWeight > 0f)
                    {
                        weight0 /= totalWeight;
                        weight1 /= totalWeight;
                        weight2 /= totalWeight;
                    }

                    // Interpolate UV coordinates
                    float u = weight0 * u0 + weight1 * u1 + weight2 * u2;
                    float v = weight0 * v0 + weight1 * v1 + weight2 * v2;

                    // Wrap normalized UV to [0, 1) using Floor (correctly handles negative values)
                    float wrappedU = u - (float)Math.Floor(u);
                    float wrappedV = v - (float)Math.Floor(v);

                    byte finalR, finalG, finalB, finalA;

                    if (samplingMode == TextureSamplingMode.Bilinear)
                    {
                        float texelX = wrappedU * texWidth - 0.5f;
                        float texelY = wrappedV * texHeight - 0.5f;

                        int xLeft = (int)Math.Floor(texelX);
                        int yTop = (int)Math.Floor(texelY);

                        float fX = texelX - xLeft;
                        float fY = texelY - yTop;

                        int tx0 = wrapX(xLeft, texWidth);
                        int tx1 = wrapX(xLeft + 1, texWidth);
                        int ty0 = wrapX(yTop, texHeight);
                        int ty1 = wrapX(yTop + 1, texHeight);

                        int idx00 = (ty0 * texWidth + tx0) * 4;
                        int idx10 = (ty0 * texWidth + tx1) * 4;
                        int idx01 = (ty1 * texWidth + tx0) * 4;
                        int idx11 = (ty1 * texWidth + tx1) * 4;

                        byte r00 = texPixels[idx00],     g00 = texPixels[idx00 + 1],     b00 = texPixels[idx00 + 2],     a00 = texPixels[idx00 + 3];
                        byte r10 = texPixels[idx10],     g10 = texPixels[idx10 + 1],     b10 = texPixels[idx10 + 2],     a10 = texPixels[idx10 + 3];
                        byte r01 = texPixels[idx01],     g01 = texPixels[idx01 + 1],     b01 = texPixels[idx01 + 2],     a01 = texPixels[idx01 + 3];
                        byte r11 = texPixels[idx11],     g11 = texPixels[idx11 + 1],     b11 = texPixels[idx11 + 2],     a11 = texPixels[idx11 + 3];

                        float interpR = (1f - fY) * ((1f - fX) * r00 + fX * r10) + fY * ((1f - fX) * r01 + fX * r11);
                        float interpG = (1f - fY) * ((1f - fX) * g00 + fX * g10) + fY * ((1f - fX) * g01 + fX * g11);
                        float interpB = (1f - fY) * ((1f - fX) * b00 + fX * b10) + fY * ((1f - fX) * b01 + fX * b11);
                        float interpA = (1f - fY) * ((1f - fX) * a00 + fX * a10) + fY * ((1f - fX) * a01 + fX * a11);

                        finalR = (byte)Math.Clamp((int)(interpR * intensity), 0, 255);
                        finalG = (byte)Math.Clamp((int)(interpG * intensity), 0, 255);
                        finalB = (byte)Math.Clamp((int)(interpB * intensity), 0, 255);
                        finalA = (byte)Math.Clamp((int)interpA, 0, 255);
                    }
                    else
                    {
                        int tx = (int)(wrappedU * texWidth);
                        int ty = (int)(wrappedV * texHeight);

                        tx = Math.Clamp(tx, 0, texWidth - 1);
                        ty = Math.Clamp(ty, 0, texHeight - 1);

                        int texIndex = (ty * texWidth + tx) * 4;

                        finalR = (byte)Math.Clamp((int)(texPixels[texIndex] * intensity), 0, 255);
                        finalG = (byte)Math.Clamp((int)(texPixels[texIndex + 1] * intensity), 0, 255);
                        finalB = (byte)Math.Clamp((int)(texPixels[texIndex + 2] * intensity), 0, 255);
                        finalA = texPixels[texIndex + 3];
                    }

                    // Source-over alpha blending onto target buffer
                    int targetIndex = (py * targetWidth + px) * 4;
                    byte dstR = targetBuffer[targetIndex];
                    byte dstG = targetBuffer[targetIndex + 1];
                    byte dstB = targetBuffer[targetIndex + 2];
                    byte dstA = targetBuffer[targetIndex + 3];

                    float srcA = finalA / 255f;
                    float dstA_norm = dstA / 255f;
                    float oneMinusSrcA = 1f - srcA;

                    float outA_norm = srcA + dstA_norm * oneMinusSrcA;
                    
                    byte outR, outG, outB, outA;
                    if (outA_norm > 0f)
                    {
                        outR = (byte)Math.Clamp((int)MathF.Round((finalR * srcA + dstR * dstA_norm * oneMinusSrcA) / outA_norm), 0, 255);
                        outG = (byte)Math.Clamp((int)MathF.Round((finalG * srcA + dstG * dstA_norm * oneMinusSrcA) / outA_norm), 0, 255);
                        outB = (byte)Math.Clamp((int)MathF.Round((finalB * srcA + dstB * dstA_norm * oneMinusSrcA) / outA_norm), 0, 255);
                        outA = (byte)Math.Clamp((int)MathF.Round(outA_norm * 255f), 0, 255);
                    }
                    else
                    {
                        outR = 0;
                        outG = 0;
                        outB = 0;
                        outA = 0;
                    }

                    targetBuffer[targetIndex] = outR;
                    targetBuffer[targetIndex + 1] = outG;
                    targetBuffer[targetIndex + 2] = outB;
                    targetBuffer[targetIndex + 3] = outA;
                }
            }
        }
    }
}
