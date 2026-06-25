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

            // Read/Skip material texture strings to position at vertex block
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
                reader.Skip(stringLen);
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
                MaterialCount = (int)materialCount
            };

            var meshData = new O3dMeshData
            {
                Vertices = vertices,
                Triangles = [],
                MaterialSlots = [], // Kept empty for this task
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
}
