using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.OmsiFormat.Parser;

/// <summary>
/// Implements detection of O3D model format versions and encryption states.
/// </summary>
public class O3dMetadataReader : IO3dMetadataReader
{
    /// <summary>
    /// Reads and parses version, encryption, and count metadata from the specified O3D file asynchronously.
    /// </summary>
    /// <param name="filePath">The absolute path to the O3D model file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous read operation, containing the O3D metadata read result.</returns>
    public Task<O3dMetadataReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = ReadCore(filePath, cancellationToken);
            return Task.FromResult(result);
        }
        catch (OperationCanceledException ex)
        {
            return Task.FromException<O3dMetadataReadResult>(ex);
        }
    }

    private O3dMetadataReadResult ReadCore(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new O3dMetadataReadResult
            {
                Status = O3dMetadataStatus.Failed,
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
            return new O3dMetadataReadResult
            {
                Status = O3dMetadataStatus.Failed,
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

        FileStream? fs = null;
        BoundedBinaryReader? reader = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            reader = new BoundedBinaryReader(fs);

            cancellationToken.ThrowIfCancellationRequested();
            if (reader.Length < 2)
            {
                return new O3dMetadataReadResult
                {
                    Status = O3dMetadataStatus.Invalid,
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

            const int MaxVertices = 1_000_000;
            const int MaxTriangles = 1_000_000;
            const int MaxMeshes = 100_000;
            const int MaxMaterials = 100_000;
            const int MaxStringLength = 1024;

            if (firstWord == 0x1984)
            {
                if (reader.RemainingBytes < 1)
                {
                    return new O3dMetadataReadResult
                    {
                        Status = O3dMetadataStatus.Invalid,
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
                        return new O3dMetadataReadResult
                        {
                            Status = O3dMetadataStatus.Invalid,
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
                    return new O3dMetadataReadResult
                    {
                        Status = O3dMetadataStatus.Encrypted,
                        Metadata = new O3dMetadata
                        {
                            Version = O3dFormatVersion.Unknown,
                            RawVersion = version,
                            IsEncrypted = true
                        },
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Warning,
                                Code = O3dDiagnosticCode.EncryptedFile,
                                Message = "O3D file encryption detected.",
                                ByteOffset = 0
                            }
                        }
                    };
                }

                uint vertexCount = 0;
                uint triangleCount = 0;
                uint materialCount = 0;
                var textureReferences = new List<O3dTextureReference>();

                while (reader.RemainingBytes > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte section = reader.ReadByte();
                    if (section == 0x17) // Vertex list
                    {
                        uint count = lHeader ? reader.ReadUInt32() : reader.ReadUInt16();
                        if (count > MaxVertices)
                        {
                            return new O3dMetadataReadResult
                            {
                                Status = O3dMetadataStatus.Invalid,
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
                        vertexCount = count;
                        long bytesToSkip = (long)count * 32;
                        if (bytesToSkip > reader.RemainingBytes)
                        {
                            return new O3dMetadataReadResult
                            {
                                Status = O3dMetadataStatus.Invalid,
                                Diagnostics = new List<O3dDiagnostic>
                                {
                                    new()
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.TruncatedStream,
                                        Message = "Truncated stream in vertex section.",
                                        ByteOffset = reader.Position
                                    }
                                }
                            };
                        }
                        reader.Skip(bytesToSkip);
                    }
                    else if (section == 0x49) // Triangle list
                    {
                        uint count = lHeader ? reader.ReadUInt32() : reader.ReadUInt16();
                        if (count > MaxTriangles)
                        {
                            return new O3dMetadataReadResult
                            {
                                Status = O3dMetadataStatus.Invalid,
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
                        triangleCount = count;
                        int recordSize = (lHeader && ((options & 1) == 1)) ? 14 : 8;
                        long bytesToSkip = (long)count * recordSize;
                        if (bytesToSkip > reader.RemainingBytes)
                        {
                            return new O3dMetadataReadResult
                            {
                                Status = O3dMetadataStatus.Invalid,
                                Diagnostics = new List<O3dDiagnostic>
                                {
                                    new()
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.TruncatedStream,
                                        Message = "Truncated stream in triangle section.",
                                        ByteOffset = reader.Position
                                    }
                                }
                            };
                        }
                        reader.Skip(bytesToSkip);
                    }
                    else if (section == 0x26) // Material list
                    {
                        if (reader.RemainingBytes < 2)
                        {
                            return new O3dMetadataReadResult
                            {
                                Status = O3dMetadataStatus.Invalid,
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
                        if ((int)count > MaxMaterials)
                        {
                            return new O3dMetadataReadResult
                            {
                                Status = O3dMetadataStatus.Invalid,
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
                        materialCount = count;
                        for (int i = 0; i < count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (reader.RemainingBytes < 45)
                            {
                                return new O3dMetadataReadResult
                                {
                                    Status = O3dMetadataStatus.Invalid,
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
                            if (stringLenByte > 0)
                            {
                                if ((int)stringLenByte > MaxStringLength)
                                {
                                    return new O3dMetadataReadResult
                                    {
                                        Status = O3dMetadataStatus.Invalid,
                                        Diagnostics = new List<O3dDiagnostic>
                                        {
                                            new()
                                            {
                                                Severity = O3dDiagnosticSeverity.Error,
                                                Code = O3dDiagnosticCode.StringLengthExceeded,
                                                Message = $"String length ({stringLenByte}) exceeds maximum allowed limit ({MaxStringLength}) for material texture {i}.",
                                                ByteOffset = reader.Position
                                            }
                                        }
                                    };
                                }
                                if (stringLenByte > reader.RemainingBytes)
                                {
                                    return new O3dMetadataReadResult
                                    {
                                        Status = O3dMetadataStatus.Invalid,
                                        Diagnostics = new List<O3dDiagnostic>
                                        {
                                            new()
                                            {
                                                Severity = O3dDiagnosticSeverity.Error,
                                                Code = O3dDiagnosticCode.InvalidStringBounds,
                                                Message = $"String length ({stringLenByte}) exceeds remaining stream bytes.",
                                                ByteOffset = reader.Position
                                            }
                                        }
                                    };
                                }
                                string path = reader.ReadBoundedString(stringLenByte, MaxStringLength, System.Text.Encoding.ASCII);
                                if (!string.IsNullOrWhiteSpace(path))
                                {
                                    textureReferences.Add(new O3dTextureReference { Path = path });
                                }
                            }
                        }
                    }
                    else if (section == 0x54) // Bone list
                    {
                        uint count = lHeader ? reader.ReadUInt32() : reader.ReadUInt16();
                        for (int i = 0; i < count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (reader.RemainingBytes < 1)
                            {
                                return new O3dMetadataReadResult
                                {
                                    Status = O3dMetadataStatus.Invalid,
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
                                return new O3dMetadataReadResult
                                {
                                    Status = O3dMetadataStatus.Invalid,
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
                                return new O3dMetadataReadResult
                                {
                                    Status = O3dMetadataStatus.Invalid,
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
                                return new O3dMetadataReadResult
                                {
                                    Status = O3dMetadataStatus.Invalid,
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
                            return new O3dMetadataReadResult
                            {
                                Status = O3dMetadataStatus.Invalid,
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
                        reader.Skip(64);
                    }
                    else
                    {
                        break;
                    }
                }

                return new O3dMetadataReadResult
                {
                    Status = O3dMetadataStatus.Success,
                    Metadata = new O3dMetadata
                    {
                        Version = lHeader ? O3dFormatVersion.Version3 : O3dFormatVersion.Legacy,
                        RawVersion = version,
                        IsEncrypted = false,
                        MeshCount = 1,
                        VertexCount = (int)vertexCount,
                        TriangleCount = (int)triangleCount,
                        MaterialCount = (int)materialCount,
                        TextureReferences = textureReferences
                    }
                };
            }
            else if (firstWord == 0x4E45)
            {
                if (reader.RemainingBytes >= 2)
                {
                    ushort secondWord = reader.ReadUInt16();
                    if (secondWord == 0x5243)
                    {
                        return new O3dMetadataReadResult
                        {
                            Status = O3dMetadataStatus.Encrypted,
                            Metadata = new O3dMetadata
                            {
                                Version = O3dFormatVersion.Unknown,
                                RawVersion = 0,
                                IsEncrypted = true
                            },
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

            cancellationToken.ThrowIfCancellationRequested();
            if (firstWord == 1 || firstWord == 2)
            {
                // Legacy short header
                if (reader.RemainingBytes < 8)
                {
                    return new O3dMetadataReadResult
                    {
                        Status = O3dMetadataStatus.Invalid,
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

                ushort meshCount = reader.ReadUInt16();
                ushort vertexCount = reader.ReadUInt16();
                ushort triangleCount = reader.ReadUInt16();
                ushort materialCount = reader.ReadUInt16();

                if ((int)meshCount > MaxMeshes || (int)vertexCount > MaxVertices || (int)triangleCount > MaxTriangles || (int)materialCount > MaxMaterials)
                {
                    return new O3dMetadataReadResult
                    {
                        Status = O3dMetadataStatus.Invalid,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.SafetyLimitExceeded,
                                Message = $"O3D legacy header counts exceeded safety limit. Meshes: {meshCount}, Vertices: {vertexCount}, Triangles: {triangleCount}, Materials: {materialCount}.",
                                ByteOffset = reader.Position
                            }
                        }
                    };
                }

                var textures = new List<O3dTextureReference>();
                for (int i = 0; i < materialCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reader.RemainingBytes < 2)
                    {
                        return new O3dMetadataReadResult
                        {
                            Status = O3dMetadataStatus.Invalid,
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

                    ushort stringLen = reader.ReadUInt16();
                    if (stringLen > MaxStringLength)
                    {
                        return new O3dMetadataReadResult
                        {
                            Status = O3dMetadataStatus.Invalid,
                            Diagnostics = new List<O3dDiagnostic>
                            {
                                new()
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.StringLengthExceeded,
                                    Message = $"String length ({stringLen}) exceeds maximum allowed limit ({MaxStringLength}) for legacy texture reference {i + 1}.",
                                    ByteOffset = reader.Position
                                }
                            }
                        };
                    }

                    if (stringLen > reader.RemainingBytes)
                    {
                        return new O3dMetadataReadResult
                        {
                            Status = O3dMetadataStatus.Invalid,
                            Diagnostics = new List<O3dDiagnostic>
                            {
                                new()
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.InvalidStringBounds,
                                    Message = $"String length ({stringLen}) exceeds remaining stream bytes ({reader.RemainingBytes}) for legacy texture reference {i + 1}.",
                                    ByteOffset = reader.Position
                                }
                            }
                        };
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    string texPath = reader.ReadBoundedString(stringLen, MaxStringLength, System.Text.Encoding.ASCII);
                    textures.Add(new O3dTextureReference { Path = texPath });
                }

                cancellationToken.ThrowIfCancellationRequested();
                return new O3dMetadataReadResult
                {
                    Status = O3dMetadataStatus.Success,
                    Metadata = new O3dMetadata
                    {
                        Version = O3dFormatVersion.Legacy,
                        RawVersion = firstWord,
                        IsEncrypted = false,
                        MeshCount = meshCount,
                        VertexCount = vertexCount,
                        TriangleCount = triangleCount,
                        MaterialCount = materialCount,
                        TextureReferences = textures
                    }
                };
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (firstWord == 3 || firstWord == 4)
            {
                if (reader.RemainingBytes < 2)
                {
                    return new O3dMetadataReadResult
                    {
                        Status = O3dMetadataStatus.Invalid,
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
                    return new O3dMetadataReadResult
                    {
                        Status = O3dMetadataStatus.Unsupported,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Warning,
                                Code = O3dDiagnosticCode.UnsupportedVersion,
                                Message = $"Unsupported version header pattern: Word1={firstWord}, Word2={secondWord}",
                                ByteOffset = 0
                            }
                        }
                    };
                }

                if (reader.RemainingBytes < 16)
                {
                    return new O3dMetadataReadResult
                    {
                        Status = O3dMetadataStatus.Invalid,
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

                uint meshCount = reader.ReadUInt32();
                uint vertexCount = reader.ReadUInt32();
                uint triangleCount = reader.ReadUInt32();
                uint materialCount = reader.ReadUInt32();

                if (meshCount > MaxMeshes || vertexCount > MaxVertices || triangleCount > MaxTriangles || materialCount > MaxMaterials)
                {
                    return new O3dMetadataReadResult
                    {
                        Status = O3dMetadataStatus.Invalid,
                        Diagnostics = new List<O3dDiagnostic>
                        {
                            new()
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.SafetyLimitExceeded,
                                Message = $"O3D long header counts exceeded safety limit. Meshes: {meshCount}, Vertices: {vertexCount}, Triangles: {triangleCount}, Materials: {materialCount}.",
                                ByteOffset = reader.Position
                            }
                        }
                    };
                }

                var textures = new List<O3dTextureReference>();
                for (int i = 0; i < (int)materialCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reader.RemainingBytes < 4)
                    {
                        return new O3dMetadataReadResult
                        {
                            Status = O3dMetadataStatus.Invalid,
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

                    uint stringLen = reader.ReadUInt32();
                    if (stringLen > MaxStringLength)
                    {
                        return new O3dMetadataReadResult
                        {
                            Status = O3dMetadataStatus.Invalid,
                            Diagnostics = new List<O3dDiagnostic>
                            {
                                new()
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.StringLengthExceeded,
                                    Message = $"String length ({stringLen}) exceeds maximum allowed limit ({MaxStringLength}) for texture reference {i + 1}.",
                                    ByteOffset = reader.Position
                                }
                            }
                        };
                    }

                    if (stringLen > reader.RemainingBytes)
                    {
                        return new O3dMetadataReadResult
                        {
                            Status = O3dMetadataStatus.Invalid,
                            Diagnostics = new List<O3dDiagnostic>
                            {
                                new()
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.InvalidStringBounds,
                                    Message = $"String length ({stringLen}) exceeds remaining stream bytes ({reader.RemainingBytes}) for texture reference {i + 1}.",
                                    ByteOffset = reader.Position
                                }
                            }
                        };
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    string texPath = reader.ReadBoundedString((int)stringLen, MaxStringLength, System.Text.Encoding.ASCII);
                    textures.Add(new O3dTextureReference { Path = texPath });
                }

                cancellationToken.ThrowIfCancellationRequested();
                var formatVersion = firstWord == 3 ? O3dFormatVersion.Version3 : O3dFormatVersion.Version4;
                return new O3dMetadataReadResult
                {
                    Status = O3dMetadataStatus.Success,
                    Metadata = new O3dMetadata
                    {
                        Version = formatVersion,
                        RawVersion = firstWord,
                        IsEncrypted = false,
                        MeshCount = (int)meshCount,
                        VertexCount = (int)vertexCount,
                        TriangleCount = (int)triangleCount,
                        MaterialCount = (int)materialCount,
                        TextureReferences = textures
                    }
                };
            }

            // Unrecognized version word
            return new O3dMetadataReadResult
            {
                Status = O3dMetadataStatus.Unsupported,
                Diagnostics = new List<O3dDiagnostic>
                {
                    new()
                    {
                        Severity = O3dDiagnosticSeverity.Warning,
                        Code = O3dDiagnosticCode.UnsupportedVersion,
                        Message = $"Unsupported or unrecognized O3D format version: {firstWord}.",
                        ByteOffset = 0
                    }
                }
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EndOfStreamException ex)
        {
            return new O3dMetadataReadResult
            {
                Status = O3dMetadataStatus.Invalid,
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
            return new O3dMetadataReadResult
            {
                Status = O3dMetadataStatus.Failed,
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

    /// <summary>
    /// Reads and parses version and encryption status from the specified O3D file.
    /// </summary>
    /// <param name="filePath">The absolute path to the O3D model file.</param>
    /// <returns>An inspection result containing O3D metadata status and diagnostics.</returns>
    public O3dMetadataReadResult Read(string filePath)
    {
        return ReadCore(filePath, CancellationToken.None);
    }
}
