using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.OmsiFormat.Parser;

/// <summary>
/// Implements concrete IO3dGeometryReader service to parse vertex, face, and material geometry from O3D files.
/// </summary>
public class O3dGeometryReader : IO3dGeometryReader
{
    /// <summary>
    /// Reads and parses version, encryption, vertex, face, and material geometry data from the specified O3D file asynchronously.
    /// </summary>
    /// <param name="filePath">The absolute path to the O3D model file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous read operation, containing the O3D geometry read result.</returns>
    public Task<O3dGeometryReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = ReadCore(filePath, cancellationToken);
            return Task.FromResult(result);
        }
        catch (OperationCanceledException ex)
        {
            return Task.FromException<O3dGeometryReadResult>(ex);
        }
    }

    /// <summary>
    /// Opens the specified file as a stream. Can be overridden in tests to inject custom streams.
    /// </summary>
    protected virtual Stream OpenFile(string filePath)
    {
        return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    private O3dGeometryReadResult ReadCore(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new O3dGeometryReadResult
            {
                Status = O3dGeometryStatus.Failed,
                Diagnostics = new List<O3dDiagnostic>
                {
                    new()
                    {
                        Severity = O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.ReadFailed,
                        Message = "File path is null or empty."
                    }
                }
            };
        }

        if (!File.Exists(filePath))
        {
            return new O3dGeometryReadResult
            {
                Status = O3dGeometryStatus.Failed,
                Diagnostics = new List<O3dDiagnostic>
                {
                    new()
                    {
                        Severity = O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.ReadFailed,
                        Message = $"File does not exist: {filePath}"
                    }
                }
            };
        }

        Stream? fs = null;
        BoundedBinaryReader? reader = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            fs = OpenFile(filePath);
            reader = new BoundedBinaryReader(fs);

            cancellationToken.ThrowIfCancellationRequested();
            if (reader.Length < 2)
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Invalid,
                    Diagnostics = new List<O3dDiagnostic>
                    {
                        new()
                        {
                            Severity = O3dDiagnosticSeverity.Error,
                            Code = O3dDiagnosticCode.TruncatedStream,
                            Message = "O3D file is too short to contain version header."
                        }
                    }
                };
            }

            ushort firstWord = reader.ReadUInt16();

            cancellationToken.ThrowIfCancellationRequested();
            // Check encrypted magic 'ENCR' (E=0x45, N=0x4E -> 0x4E45. C=0x43, R=0x52 -> 0x5243)
            if (firstWord == 0x4E45)
            {
                if (reader.RemainingBytes >= 2)
                {
                    ushort secondWord = reader.ReadUInt16();
                    if (secondWord == 0x5243)
                    {
                        return new O3dGeometryReadResult
                        {
                            Status = O3dGeometryStatus.Encrypted,
                            Diagnostics = new List<O3dDiagnostic>
                            {
                                new()
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.EncryptedFile,
                                    Message = "O3D file encryption detected.",
                                    ByteOffset = 0
                                }
                            }
                        };
                    }
                }
            }

            long meshCount = 0;
            long vertexCount = 0;
            long triangleCount = 0;
            long materialCount = 0;
            O3dFormatVersion formatVersion = O3dFormatVersion.Unknown;

            cancellationToken.ThrowIfCancellationRequested();
            if (firstWord == 1 || firstWord == 2)
            {
                formatVersion = O3dFormatVersion.Legacy;
                if (reader.RemainingBytes < 8)
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Invalid,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.TruncatedStream,
                                Message = $"Truncated stream: expected 8 bytes for legacy counts but only {reader.RemainingBytes} remain.",
                                ByteOffset = reader.Position
                            }
                        }
                    };
                }

                meshCount = reader.ReadUInt16();
                vertexCount = reader.ReadUInt16();
                triangleCount = reader.ReadUInt16();
                materialCount = reader.ReadUInt16();
            }
            else if (firstWord == 3 || firstWord == 4)
            {
                formatVersion = firstWord == 3 ? O3dFormatVersion.Version3 : O3dFormatVersion.Version4;
                if (reader.RemainingBytes < 2)
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Invalid,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.TruncatedStream,
                                Message = "O3D stream ended while reading Version 3/4 long header word.",
                                ByteOffset = reader.Position
                            }
                        }
                    };
                }

                ushort secondWord = reader.ReadUInt16();
                if (secondWord != 0)
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Unsupported,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.UnsupportedVersion,
                                Message = $"Unsupported version header pattern: Word1={firstWord}, Word2={secondWord}",
                                ByteOffset = 0
                            }
                        }
                    };
                }

                if (reader.RemainingBytes < 16)
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Invalid,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.TruncatedStream,
                                Message = $"Truncated stream: expected 16 bytes for long count fields but only {reader.RemainingBytes} remain.",
                                ByteOffset = reader.Position
                            }
                        }
                    };
                }

                meshCount = reader.ReadUInt32();
                vertexCount = reader.ReadUInt32();
                triangleCount = reader.ReadUInt32();
                materialCount = reader.ReadUInt32();
            }
            else if (firstWord == 0x1984)
            {
                if (reader.RemainingBytes < 1)
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Invalid,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.TruncatedStream,
                                Message = "O3D stream ended before version byte could be read.",
                                ByteOffset = reader.Position
                            }
                        }
                    };
                }
                byte version = reader.ReadByte();
                bool lHeader = version > 3;
                bool isEncrypted = false;
                byte options = 0;

                if (lHeader)
                {
                    if (reader.RemainingBytes < 5)
                    {
                        return new O3dGeometryReadResult
                        {
                            Status = O3dGeometryStatus.Invalid,
                            Diagnostics = new List<O3dDiagnostic>
                            {
                                new()
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.TruncatedStream,
                                    Message = "O3D stream ended before extended header could be read.",
                                    ByteOffset = reader.Position
                                }
                            }
                        };
                    }
                    options = reader.ReadByte();
                    uint encryptionKey = reader.ReadUInt32();
                    if (encryptionKey != 0xffffffff)
                    {
                        isEncrypted = true;
                    }
                }

                if (isEncrypted)
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Encrypted,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.EncryptedFile,
                                Message = "O3D file encryption detected.",
                                ByteOffset = 0
                            }
                        }
                    };
                }

                formatVersion = lHeader ? O3dFormatVersion.Version3 : O3dFormatVersion.Legacy;
                meshCount = 1;

                var vertices1984 = new List<O3dVertex>();
                var triangles1984 = new List<O3dTriangle>();
                var materialSlots1984 = new List<O3dMaterialSlot>();
                var diagnostics1984 = new List<O3dDiagnostic>();

                uint parsedVertexCount = 0;
                uint parsedTriangleCount = 0;
                uint parsedMaterialCount = 0;

                while (reader.RemainingBytes > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte section = reader.ReadByte();
                    if (section == 0x17) // Vertex list
                    {
                        if (reader.RemainingBytes < (lHeader ? 4 : 2))
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Invalid,
                                Diagnostics = new List<O3dDiagnostic>
                                {
                                    new()
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.TruncatedStream,
                                        Message = "Truncated stream in vertex section count.",
                                        ByteOffset = reader.Position
                                    }
                                }
                            };
                        }
                        uint count = lHeader ? reader.ReadUInt32() : reader.ReadUInt16();
                        if (count > O3dGeometrySafetyPolicy.MaxVertices)
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Invalid,
                                Diagnostics = new List<O3dDiagnostic>
                                {
                                    new()
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.SafetyLimitExceeded,
                                        Message = $"O3D vertex count exceeded safety limit: {count}.",
                                        ByteOffset = reader.Position
                                    }
                                }
                            };
                        }
                        parsedVertexCount = count;

                        if (!O3dGeometrySafetyPolicy.ValidateVertexBlock(parsedVertexCount, reader.RemainingBytes, reader.Position, out var vertexDiag1984))
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Invalid,
                                Diagnostics = vertexDiag1984 != null ? new List<O3dDiagnostic> { vertexDiag1984 } : []
                            };
                        }

                        for (int i = 0; i < (int)parsedVertexCount; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            float x = reader.ReadSingle();
                            float y = reader.ReadSingle();
                            float z = reader.ReadSingle();
                            float nx = reader.ReadSingle();
                            float ny = reader.ReadSingle();
                            float nz = reader.ReadSingle();
                            float u = reader.ReadSingle();
                            float v = reader.ReadSingle();

                            vertices1984.Add(new O3dVertex
                            {
                                X = x,
                                Y = y,
                                Z = z,
                                Normal = new O3dNormal { X = nx, Y = ny, Z = nz },
                                Uv = new O3dUv { U = u, V = v }
                            });
                        }
                    }
                    else if (section == 0x49) // Triangle list
                    {
                        if (reader.RemainingBytes < (lHeader ? 4 : 2))
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Invalid,
                                Diagnostics = new List<O3dDiagnostic>
                                {
                                    new()
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.TruncatedStream,
                                        Message = "Truncated stream in triangle section count.",
                                        ByteOffset = reader.Position
                                    }
                                }
                            };
                        }
                        uint count = lHeader ? reader.ReadUInt32() : reader.ReadUInt16();
                        if (count > O3dGeometrySafetyPolicy.MaxTriangles)
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Invalid,
                                Diagnostics = new List<O3dDiagnostic>
                                {
                                    new()
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.SafetyLimitExceeded,
                                        Message = $"O3D triangle count exceeded safety limit: {count}.",
                                        ByteOffset = reader.Position
                                    }
                                }
                            };
                        }
                        parsedTriangleCount = count;
                        bool useLongIndices1984 = lHeader && ((options & 1) == 1);

                        if (!O3dGeometrySafetyPolicy.ValidateFaceBlock(parsedTriangleCount, useLongIndices1984, reader.RemainingBytes, reader.Position, out var faceDiag1984))
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Invalid,
                                Diagnostics = faceDiag1984 != null ? new List<O3dDiagnostic> { faceDiag1984 } : []
                            };
                        }

                        for (int i = 0; i < (int)parsedTriangleCount; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            long recordOffset = reader.Position;
                            long v0Raw, v1Raw, v2Raw;
                            ushort materialIndex;

                            if (useLongIndices1984)
                            {
                                v0Raw = reader.ReadUInt32();
                                v1Raw = reader.ReadUInt32();
                                v2Raw = reader.ReadUInt32();
                                materialIndex = reader.ReadUInt16();
                            }
                            else
                            {
                                v0Raw = reader.ReadUInt16();
                                v1Raw = reader.ReadUInt16();
                                v2Raw = reader.ReadUInt16();
                                materialIndex = reader.ReadUInt16();
                            }

                            triangles1984.Add(new O3dTriangle
                            {
                                V0 = (int)v0Raw,
                                V1 = (int)v1Raw,
                                V2 = (int)v2Raw,
                                MaterialSlotIndex = materialIndex
                            });
                        }
                    }
                    else if (section == 0x26) // Material list
                    {
                        if (reader.RemainingBytes < 2)
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Invalid,
                                Diagnostics = new List<O3dDiagnostic>
                                {
                                    new()
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.TruncatedStream,
                                        Message = "Truncated stream while reading material count.",
                                        ByteOffset = reader.Position
                                    }
                                }
                            };
                        }
                        ushort count = reader.ReadUInt16();
                        if ((int)count > O3dGeometrySafetyPolicy.MaxMaterials)
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Invalid,
                                Diagnostics = new List<O3dDiagnostic>
                                {
                                    new()
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.SafetyLimitExceeded,
                                        Message = $"O3D material count exceeded safety limit: {count}.",
                                        ByteOffset = reader.Position
                                    }
                                }
                            };
                        }
                        parsedMaterialCount = count;

                        for (int i = 0; i < count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (reader.RemainingBytes < 45)
                            {
                                return new O3dGeometryReadResult
                                {
                                    Status = O3dGeometryStatus.Invalid,
                                    Diagnostics = new List<O3dDiagnostic>
                                    {
                                        new()
                                        {
                                            Severity = O3dDiagnosticSeverity.Error,
                                            Code = O3dDiagnosticCode.TruncatedStream,
                                            Message = $"Truncated stream in material list at index {i}.",
                                            ByteOffset = reader.Position
                                        }
                                    }
                                };
                            }
                            reader.Skip(44);
                            byte stringLenByte = reader.ReadByte();
                            string? texPath = null;
                            if (stringLenByte > 0)
                            {
                                if (!O3dGeometrySafetyPolicy.ValidateStringLength(stringLenByte, reader.RemainingBytes, reader.Position, out var stringDiag1984))
                                {
                                    return new O3dGeometryReadResult
                                    {
                                        Status = O3dGeometryStatus.Invalid,
                                        Diagnostics = stringDiag1984 != null ? new List<O3dDiagnostic> { stringDiag1984 } : []
                                    };
                                }

                                cancellationToken.ThrowIfCancellationRequested();
                                texPath = reader.ReadBoundedString(stringLenByte, O3dGeometrySafetyPolicy.MaxStringLength, System.Text.Encoding.ASCII);
                            }

                            materialSlots1984.Add(new O3dMaterialSlot
                            {
                                MaterialName = $"Material {i}",
                                TextureReference = string.IsNullOrWhiteSpace(texPath) ? null : texPath
                            });
                        }
                    }
                    else if (section == 0x54) // Bone list
                    {
                        if (reader.RemainingBytes < (lHeader ? 4 : 2))
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Invalid,
                                Diagnostics = new List<O3dDiagnostic>
                                {
                                    new()
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.TruncatedStream,
                                        Message = "Truncated stream in bone list count.",
                                        ByteOffset = reader.Position
                                    }
                                }
                            };
                        }
                        uint count = lHeader ? reader.ReadUInt32() : reader.ReadUInt16();
                        diagnostics1984.Add(new O3dDiagnostic
                        {
                            Severity = O3dDiagnosticSeverity.Warning,
                            Code = O3dDiagnosticCode.Unknown,
                            Message = $"Unsupported section bone list (0x54) skipped. Count: {count}.",
                            ByteOffset = reader.Position - (lHeader ? 5 : 3)
                        });

                        for (int i = 0; i < count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (reader.RemainingBytes < 1)
                            {
                                return new O3dGeometryReadResult
                                {
                                    Status = O3dGeometryStatus.Invalid,
                                    Diagnostics = new List<O3dDiagnostic>
                                    {
                                        new()
                                        {
                                            Severity = O3dDiagnosticSeverity.Error,
                                            Code = O3dDiagnosticCode.TruncatedStream,
                                            Message = "Truncated stream in bone list.",
                                            ByteOffset = reader.Position
                                        }
                                    }
                                };
                            }
                            byte nameLen = reader.ReadByte();
                            if (nameLen > reader.RemainingBytes)
                            {
                                return new O3dGeometryReadResult
                                {
                                    Status = O3dGeometryStatus.Invalid,
                                    Diagnostics = new List<O3dDiagnostic>
                                    {
                                        new()
                                        {
                                            Severity = O3dDiagnosticSeverity.Error,
                                            Code = O3dDiagnosticCode.TruncatedStream,
                                            Message = "Truncated stream in bone name.",
                                            ByteOffset = reader.Position
                                        }
                                    }
                                };
                            }
                            reader.Skip(nameLen);
                            if (reader.RemainingBytes < 2)
                            {
                                return new O3dGeometryReadResult
                                {
                                    Status = O3dGeometryStatus.Invalid,
                                    Diagnostics = new List<O3dDiagnostic>
                                    {
                                        new()
                                        {
                                            Severity = O3dDiagnosticSeverity.Error,
                                            Code = O3dDiagnosticCode.TruncatedStream,
                                            Message = "Truncated stream in bone weights count.",
                                            ByteOffset = reader.Position
                                        }
                                    }
                                };
                            }
                            ushort nWeights = reader.ReadUInt16();
                            long weightsBytes = (long)nWeights * 6;
                            if (weightsBytes > reader.RemainingBytes)
                            {
                                return new O3dGeometryReadResult
                                {
                                    Status = O3dGeometryStatus.Invalid,
                                    Diagnostics = new List<O3dDiagnostic>
                                    {
                                        new()
                                        {
                                            Severity = O3dDiagnosticSeverity.Error,
                                            Code = O3dDiagnosticCode.TruncatedStream,
                                            Message = "Truncated stream in bone weights.",
                                            ByteOffset = reader.Position
                                        }
                                    }
                                };
                            }
                            reader.Skip(weightsBytes);
                        }
                    }
                    else if (section == 0x79) // Transform
                    {
                        if (reader.RemainingBytes < 64)
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Invalid,
                                Diagnostics = new List<O3dDiagnostic>
                                {
                                    new()
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.TruncatedStream,
                                        Message = "Truncated stream in transform matrix.",
                                        ByteOffset = reader.Position
                                    }
                                }
                            };
                        }
                        diagnostics1984.Add(new O3dDiagnostic
                        {
                            Severity = O3dDiagnosticSeverity.Warning,
                            Code = O3dDiagnosticCode.Unknown,
                            Message = "Unsupported section transform (0x79) skipped.",
                            ByteOffset = reader.Position - 1
                        });
                        reader.Skip(64);
                    }
                    else
                    {
                        diagnostics1984.Add(new O3dDiagnostic
                        {
                            Severity = O3dDiagnosticSeverity.Warning,
                            Code = O3dDiagnosticCode.Unknown,
                            Message = $"Unrecognized section byte 0x{section:X2} encountered. Stopping section parse.",
                            ByteOffset = reader.Position - 1
                        });
                        break;
                    }
                }

                // Consistency & index validations
                foreach (var triangle in triangles1984)
                {
                    if (triangle.MaterialSlotIndex < 0 || triangle.MaterialSlotIndex >= materialSlots1984.Count)
                    {
                        return new O3dGeometryReadResult
                        {
                            Status = O3dGeometryStatus.Invalid,
                            Diagnostics = new List<O3dDiagnostic>
                            {
                                new()
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.InvalidIndex,
                                    Message = $"Consistency check failed: triangle material slot index {triangle.MaterialSlotIndex} is out of bounds for the material slots collection (Count: {materialSlots1984.Count})."
                                }
                            }
                        };
                    }

                    if (triangle.V0 < 0 || triangle.V0 >= vertices1984.Count ||
                        triangle.V1 < 0 || triangle.V1 >= vertices1984.Count ||
                        triangle.V2 < 0 || triangle.V2 >= vertices1984.Count)
                    {
                        return new O3dGeometryReadResult
                        {
                            Status = O3dGeometryStatus.Invalid,
                            Diagnostics = new List<O3dDiagnostic>
                            {
                                new()
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.InvalidIndex,
                                    Message = $"Consistency check failed: triangle vertex indices ({triangle.V0}, {triangle.V1}, {triangle.V2}) are out of bounds for the vertices collection (Count: {vertices1984.Count})."
                                }
                            }
                        };
                    }
                }

                var textureReferences1984 = new List<O3dTextureReference>();
                foreach (var slot in materialSlots1984)
                {
                    if (!string.IsNullOrWhiteSpace(slot.TextureReference))
                    {
                        textureReferences1984.Add(new O3dTextureReference { Path = slot.TextureReference });
                    }
                }

                var metadata1984 = new O3dMetadata
                {
                    Version = formatVersion,
                    RawVersion = version,
                    IsEncrypted = false,
                    MeshCount = (int)meshCount,
                    VertexCount = (int)parsedVertexCount,
                    TriangleCount = (int)parsedTriangleCount,
                    MaterialCount = (int)parsedMaterialCount,
                    TextureReferences = textureReferences1984
                };

                var meshData1984 = new O3dMeshData
                {
                    Vertices = vertices1984,
                    Triangles = triangles1984,
                    MaterialSlots = materialSlots1984,
                    Metadata = metadata1984
                };

                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Success,
                    MeshData = meshData1984,
                    Diagnostics = diagnostics1984
                };
            }
            else
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Unsupported,
                    Diagnostics = new List<O3dDiagnostic>
                    {
                        new()
                        {
                            Severity = O3dDiagnosticSeverity.Error,
                            Code = O3dDiagnosticCode.UnsupportedVersion,
                            Message = $"Unsupported or unrecognized O3D format version: {firstWord}.",
                            ByteOffset = 0
                        }
                    }
                };
            }

            cancellationToken.ThrowIfCancellationRequested();
            // Validate counts using safety policy
            if (!O3dGeometrySafetyPolicy.ValidateCounts(meshCount, vertexCount, triangleCount, materialCount, reader.Position, out var countDiag))
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Invalid,
                    Diagnostics = countDiag != null ? new List<O3dDiagnostic> { countDiag } : []
                };
            }

            // Read material texture strings
            var materialSlots = new List<O3dMaterialSlot>();
            for (int i = 0; i < (int)materialCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long stringLen;
                if (formatVersion == O3dFormatVersion.Legacy)
                {
                    if (reader.RemainingBytes < 2)
                    {
                        return new O3dGeometryReadResult
                        {
                            Status = O3dGeometryStatus.Invalid,
                            Diagnostics = new List<O3dDiagnostic>
                            {
                                new()
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.TruncatedStream,
                                    Message = $"Truncated stream while reading legacy string length prefix for texture reference {i + 1}.",
                                    ByteOffset = reader.Position
                                }
                            }
                        };
                    }
                    stringLen = reader.ReadUInt16();
                }
                else
                {
                    if (reader.RemainingBytes < 4)
                    {
                        return new O3dGeometryReadResult
                        {
                            Status = O3dGeometryStatus.Invalid,
                            Diagnostics = new List<O3dDiagnostic>
                            {
                                new()
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.TruncatedStream,
                                    Message = $"Truncated stream while reading string length prefix for texture reference {i + 1}.",
                                    ByteOffset = reader.Position
                                }
                            }
                        };
                    }
                    stringLen = reader.ReadUInt32();
                }

                if (!O3dGeometrySafetyPolicy.ValidateStringLength(stringLen, reader.RemainingBytes, reader.Position, out var stringDiag))
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Invalid,
                        Diagnostics = stringDiag != null ? new List<O3dDiagnostic> { stringDiag } : []
                    };
                }

                cancellationToken.ThrowIfCancellationRequested();
                string texPath = reader.ReadBoundedString((int)stringLen, O3dGeometrySafetyPolicy.MaxStringLength, System.Text.Encoding.ASCII);
                materialSlots.Add(new O3dMaterialSlot
                {
                    MaterialName = $"Material {i}",
                    TextureReference = string.IsNullOrWhiteSpace(texPath) ? null : texPath
                });
            }

            cancellationToken.ThrowIfCancellationRequested();
            // Validate vertex block size using safety policy
            if (!O3dGeometrySafetyPolicy.ValidateVertexBlock(vertexCount, reader.RemainingBytes, reader.Position, out var vertexDiag))
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Invalid,
                    Diagnostics = vertexDiag != null ? new List<O3dDiagnostic> { vertexDiag } : []
                };
            }

            // Read vertices
            var vertices = new List<O3dVertex>();
            for (int i = 0; i < (int)vertexCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                float z = reader.ReadSingle();
                float nx = reader.ReadSingle();
                float ny = reader.ReadSingle();
                float nz = reader.ReadSingle();
                float u = reader.ReadSingle();
                float v = reader.ReadSingle();

                vertices.Add(new O3dVertex
                {
                    X = x,
                    Y = y,
                    Z = z,
                    Normal = new O3dNormal { X = nx, Y = ny, Z = nz },
                    Uv = new O3dUv { U = u, V = v }
                });
            }

            cancellationToken.ThrowIfCancellationRequested();
            bool useLongIndices = UsesLongIndices(formatVersion);

            // Validate face block size using safety policy
            if (!O3dGeometrySafetyPolicy.ValidateFaceBlock(triangleCount, useLongIndices, reader.RemainingBytes, reader.Position, out var faceDiag))
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Invalid,
                    Diagnostics = faceDiag != null ? new List<O3dDiagnostic> { faceDiag } : []
                };
            }

            // Read faces
            var triangles = new List<O3dTriangle>();
            for (int i = 0; i < (int)triangleCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long recordOffset = reader.Position;
                long v0Raw, v1Raw, v2Raw;
                ushort materialIndex;

                if (useLongIndices)
                {
                    v0Raw = reader.ReadUInt32();
                    v1Raw = reader.ReadUInt32();
                    v2Raw = reader.ReadUInt32();
                    materialIndex = reader.ReadUInt16();
                }
                else
                {
                    v0Raw = reader.ReadUInt16();
                    v1Raw = reader.ReadUInt16();
                    v2Raw = reader.ReadUInt16();
                    materialIndex = reader.ReadUInt16();
                }

                // Validate each index is 0 <= index < vertexCount and fits in integer
                if (v0Raw > int.MaxValue || v1Raw > int.MaxValue || v2Raw > int.MaxValue ||
                    v0Raw >= vertexCount || v1Raw >= vertexCount || v2Raw >= vertexCount)
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Invalid,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.InvalidIndex,
                                Message = $"Triangle {i} has out-of-bounds or overflow vertex index: ({v0Raw}, {v1Raw}, {v2Raw}). Vertex count: {vertexCount}.",
                                ByteOffset = recordOffset
                            }
                        }
                    };
                }

                // Validate material slot index bounds
                if (materialIndex >= materialCount)
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Invalid,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.InvalidIndex,
                                Message = $"Triangle {i} has out-of-bounds material slot index: {materialIndex}. Material count: {materialCount}.",
                                ByteOffset = recordOffset
                            }
                        }
                    };
                }

                triangles.Add(new O3dTriangle
                {
                    V0 = (int)v0Raw,
                    V1 = (int)v1Raw,
                    V2 = (int)v2Raw,
                    MaterialSlotIndex = materialIndex
                });
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Populate Metadata.TextureReferences from material slots
            var textureReferences = new List<O3dTextureReference>();
            foreach (var slot in materialSlots)
            {
                if (!string.IsNullOrWhiteSpace(slot.TextureReference))
                {
                    textureReferences.Add(new O3dTextureReference { Path = slot.TextureReference });
                }
            }

            // Consistency checks
            if (vertices.Count != (int)vertexCount ||
                triangles.Count != (int)triangleCount ||
                materialSlots.Count != (int)materialCount)
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Invalid,
                    Diagnostics = new List<O3dDiagnostic>
                    {
                        new()
                        {
                            Severity = O3dDiagnosticSeverity.Error,
                            Code = O3dDiagnosticCode.InvalidIndex,
                            Message = $"Consistency check failed: actual collection counts (Vertices: {vertices.Count}, Triangles: {triangles.Count}, Materials: {materialSlots.Count}) do not match header metadata counts (Vertices: {vertexCount}, Triangles: {triangleCount}, Materials: {materialCount})."
                        }
                    }
                };
            }

            foreach (var triangle in triangles)
            {
                if (triangle.MaterialSlotIndex < 0 || triangle.MaterialSlotIndex >= materialSlots.Count)
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Invalid,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.InvalidIndex,
                                Message = $"Consistency check failed: triangle material slot index {triangle.MaterialSlotIndex} is out of bounds for the material slots collection (Count: {materialSlots.Count})."
                            }
                        }
                    };
                }

                if (triangle.V0 < 0 || triangle.V0 >= vertices.Count ||
                    triangle.V1 < 0 || triangle.V1 >= vertices.Count ||
                    triangle.V2 < 0 || triangle.V2 >= vertices.Count)
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Invalid,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.InvalidIndex,
                                Message = $"Consistency check failed: triangle vertex indices ({triangle.V0}, {triangle.V1}, {triangle.V2}) are out of bounds for the vertices collection (Count: {vertices.Count})."
                            }
                        }
                    };
                }
            }

            // Create Metadata object for O3dMeshData
            var metadata = new O3dMetadata
            {
                Version = formatVersion,
                RawVersion = formatVersion switch
                {
                    O3dFormatVersion.Legacy => 2,
                    O3dFormatVersion.Version3 => 3,
                    O3dFormatVersion.Version4 => 4,
                    _ => 0
                },
                IsEncrypted = false,
                MeshCount = (int)meshCount,
                VertexCount = (int)vertexCount,
                TriangleCount = (int)triangleCount,
                MaterialCount = (int)materialCount,
                TextureReferences = textureReferences
            };

            var meshData = new O3dMeshData
            {
                Vertices = vertices,
                Triangles = triangles,
                MaterialSlots = materialSlots,
                Metadata = metadata
            };

            return new O3dGeometryReadResult
            {
                Status = O3dGeometryStatus.Success,
                MeshData = meshData,
                Diagnostics = []
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EndOfStreamException ex)
        {
            return new O3dGeometryReadResult
            {
                Status = O3dGeometryStatus.Invalid,
                Diagnostics = new List<O3dDiagnostic>
                {
                    new()
                    {
                        Severity = O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.TruncatedStream,
                        Message = ex.Message,
                        ByteOffset = reader?.Position
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new O3dGeometryReadResult
            {
                Status = O3dGeometryStatus.Failed,
                Diagnostics = new List<O3dDiagnostic>
                {
                    new()
                    {
                        Severity = O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.ReadFailed,
                        Message = ex.Message
                    }
                }
            };
        }
        finally
        {
            reader?.Dispose();
            fs?.Dispose();
        }
    }

    private static bool UsesLongIndices(O3dFormatVersion version)
    {
        return version == O3dFormatVersion.Version4;
    }
}
