using System;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.OmsiFormat.Parser;

/// <summary>
/// Centralizes O3D geometry parsing safety rules, count validations, and bounds checks to prevent denial of service (DoS) memory exhaustion.
/// </summary>
public static class O3dGeometrySafetyPolicy
{
    /// <summary>
    /// Byte size of a single O3D vertex record.
    /// </summary>
    public const int VertexRecordSizeBytes = 32;

    /// <summary>
    /// Byte size of a standard triangle face index record (3 x uint16 indices + 1 x uint16 material).
    /// </summary>
    public const int StandardFaceRecordSizeBytes = 8;

    /// <summary>
    /// Byte size of a long triangle face index record (3 x uint32 indices + 1 x uint16 material).
    /// </summary>
    public const int LongFaceRecordSizeBytes = 14;

    /// <summary>
    /// Maximum vertices allowed for single mesh.
    /// </summary>
    public const int MaxVertices = 1_000_000;

    /// <summary>
    /// Maximum triangles allowed for single mesh.
    /// </summary>
    public const int MaxTriangles = 1_000_000;

    /// <summary>
    /// Maximum submeshes allowed.
    /// </summary>
    public const int MaxMeshes = 100_000;

    /// <summary>
    /// Maximum material slots allowed.
    /// </summary>
    public const int MaxMaterials = 100_000;

    /// <summary>
    /// Maximum length prefix allowed for texture and material names.
    /// </summary>
    public const int MaxStringLength = 1024;

    /// <summary>
    /// Validates O3D header counts against negative inputs and maximum safety thresholds.
    /// </summary>
    public static bool ValidateCounts(
        long meshCount, 
        long vertexCount, 
        long triangleCount, 
        long materialCount, 
        long byteOffset,
        out O3dDiagnostic? diagnostic)
    {
        diagnostic = null;

        if (meshCount < 0 || vertexCount < 0 || triangleCount < 0 || materialCount < 0)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.InvalidCount,
                Message = $"Header counts cannot be negative. Meshes: {meshCount}, Vertices: {vertexCount}, Triangles: {triangleCount}, Materials: {materialCount}.",
                ByteOffset = byteOffset
            };
            return false;
        }

        if (meshCount > MaxMeshes)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.SafetyLimitExceeded,
                Message = $"Mesh count ({meshCount}) exceeds the safety limit of {MaxMeshes}.",
                ByteOffset = byteOffset
            };
            return false;
        }

        if (vertexCount > MaxVertices)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.SafetyLimitExceeded,
                Message = $"Vertex count ({vertexCount}) exceeds the safety limit of {MaxVertices}.",
                ByteOffset = byteOffset
            };
            return false;
        }

        if (triangleCount > MaxTriangles)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.SafetyLimitExceeded,
                Message = $"Triangle count ({triangleCount}) exceeds the safety limit of {MaxTriangles}.",
                ByteOffset = byteOffset
            };
            return false;
        }

        if (materialCount > MaxMaterials)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.SafetyLimitExceeded,
                Message = $"Material count ({materialCount}) exceeds the safety limit of {MaxMaterials}.",
                ByteOffset = byteOffset
            };
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates the vertex block byte size using checked arithmetic and checks if it fits within the remaining stream length.
    /// </summary>
    public static bool ValidateVertexBlock(
        long vertexCount, 
        long remainingBytes, 
        long byteOffset,
        out O3dDiagnostic? diagnostic)
    {
        diagnostic = null;

        if (vertexCount < 0)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.InvalidCount,
                Message = $"Vertex count cannot be negative: {vertexCount}.",
                ByteOffset = byteOffset
            };
            return false;
        }

        long vertexBlockSize;
        try
        {
            checked
            {
                vertexBlockSize = vertexCount * VertexRecordSizeBytes;
            }
        }
        catch (OverflowException)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.SafetyLimitExceeded,
                Message = "Arithmetic overflow calculated for vertex block size.",
                ByteOffset = byteOffset
            };
            return false;
        }

        if (vertexBlockSize > remainingBytes)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.TruncatedStream,
                Message = $"Truncated stream: expected {vertexBlockSize} bytes for vertex block but only {remainingBytes} remain.",
                ByteOffset = byteOffset
            };
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates the face block byte size using checked arithmetic and checks if it fits within the remaining stream length.
    /// </summary>
    public static bool ValidateFaceBlock(
        long triangleCount, 
        bool useLongIndices, 
        long remainingBytes, 
        long byteOffset,
        out O3dDiagnostic? diagnostic)
    {
        diagnostic = null;

        if (triangleCount < 0)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.InvalidCount,
                Message = $"Triangle count cannot be negative: {triangleCount}.",
                ByteOffset = byteOffset
            };
            return false;
        }

        int recordSize = useLongIndices ? LongFaceRecordSizeBytes : StandardFaceRecordSizeBytes;
        long faceBlockSize;
        try
        {
            checked
            {
                faceBlockSize = triangleCount * recordSize;
            }
        }
        catch (OverflowException)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.SafetyLimitExceeded,
                Message = "Arithmetic overflow calculated for face block size.",
                ByteOffset = byteOffset
            };
            return false;
        }

        if (faceBlockSize > remainingBytes)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.TruncatedStream,
                Message = $"Truncated stream: expected {faceBlockSize} bytes for face block but only {remainingBytes} remain.",
                ByteOffset = byteOffset
            };
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates string bounds and caps lengths against maximum configured limits.
    /// </summary>
    public static bool ValidateStringLength(
        long stringLen, 
        long remainingBytes, 
        long byteOffset,
        out O3dDiagnostic? diagnostic)
    {
        diagnostic = null;

        if (stringLen < 0)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.InvalidCount,
                Message = $"String length cannot be negative: {stringLen}.",
                ByteOffset = byteOffset
            };
            return false;
        }

        if (stringLen > MaxStringLength)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.StringLengthExceeded,
                Message = $"String length ({stringLen}) exceeds maximum allowed limit ({MaxStringLength}).",
                ByteOffset = byteOffset
            };
            return false;
        }

        if (stringLen > remainingBytes)
        {
            diagnostic = new O3dDiagnostic
            {
                Severity = O3dDiagnosticSeverity.Error,
                Code = O3dDiagnosticCode.InvalidStringBounds,
                Message = $"String length ({stringLen}) exceeds remaining stream bytes ({remainingBytes}).",
                ByteOffset = byteOffset
            };
            return false;
        }

        return true;
    }
}
