using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmsiStudio.Core.Assets;

/// <summary>
/// A geometry reader for DirectX .x mesh files supporting ASCII text format parsing.
/// </summary>
public class DirectXGeometryReader : IDirectXGeometryReader
{
    /// <inheritdoc />
    public async Task<O3dGeometryReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Invalid,
                    Diagnostics = new[]
                    {
                        new O3dDiagnostic
                        {
                            Severity = O3dDiagnosticSeverity.Error,
                            Code = O3dDiagnosticCode.InvalidPath,
                            Message = "File path is empty."
                        }
                    }
                };
            }

            if (!File.Exists(filePath))
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Invalid,
                    Diagnostics = new[]
                    {
                        new O3dDiagnostic
                        {
                            Severity = O3dDiagnosticSeverity.Error,
                            Code = O3dDiagnosticCode.FileNotFound,
                            Message = $"File not found: {filePath}"
                        }
                    }
                };
            }

            // DoS protection: limit file size to 50 MB
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 50 * 1024 * 1024)
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Unsupported,
                    Diagnostics = new[]
                    {
                        new O3dDiagnostic
                        {
                            Severity = O3dDiagnosticSeverity.Error,
                            Code = O3dDiagnosticCode.SafetyLimitExceeded,
                            Message = "File size exceeds safety limit of 50 MB."
                        }
                    }
                };
            }

            // Read the 16-byte header to detect type (txt vs bin/compressed)
            byte[] header = new byte[16];
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int bytesRead = fs.Read(header, 0, 16);
                if (bytesRead < 16)
                {
                    return new O3dGeometryReadResult
                    {
                        Status = O3dGeometryStatus.Invalid,
                        Diagnostics = new[]
                        {
                            new O3dDiagnostic
                            {
                                Severity = O3dDiagnosticSeverity.Error,
                                Code = O3dDiagnosticCode.InvalidHeader,
                                Message = "File is too short to be a valid DirectX .x file."
                            }
                        }
                    };
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            string signature = Encoding.ASCII.GetString(header, 0, 4);
            if (signature != "xof ")
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Invalid,
                    Diagnostics = new[]
                    {
                        new O3dDiagnostic
                        {
                            Severity = O3dDiagnosticSeverity.Error,
                            Code = O3dDiagnosticCode.InvalidHeader,
                            Message = "Invalid DirectX .x file signature."
                        }
                    }
                };
            }

            string format = Encoding.ASCII.GetString(header, 8, 4);
            if (!format.StartsWith("txt", StringComparison.Ordinal))
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Unsupported,
                    Diagnostics = new[]
                    {
                        new O3dDiagnostic
                        {
                            Severity = O3dDiagnosticSeverity.Error,
                            Code = O3dDiagnosticCode.UnsupportedFormat,
                            Message = "Binary or compressed DirectX .x formats are not supported."
                        }
                    }
                };
            }

            // Read text content
            string text = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Strip comments (// and #) quote-aware
            var lines = new List<string>();
            using (var reader = new StringReader(text))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool inQuotes = false;
                    int commentStart = -1;
                    for (int i = 0; i < line.Length; i++)
                    {
                        char c = line[i];
                        if (c == '"')
                        {
                            inQuotes = !inQuotes;
                        }
                        else if (!inQuotes)
                        {
                            if (c == '#')
                            {
                                commentStart = i;
                                break;
                            }
                            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                            {
                                commentStart = i;
                                break;
                            }
                        }
                    }

                    if (commentStart >= 0)
                    {
                        line = line.Substring(0, commentStart);
                    }

                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        lines.Add(line);
                    }
                }
            }

            // 2. Tokenize
            var tokens = new List<string>();
            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sb = new StringBuilder();
                bool inQuotes = false;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c == '"')
                    {
                        inQuotes = !inQuotes;
                        sb.Append(c);
                        if (!inQuotes)
                        {
                            tokens.Add(sb.ToString());
                            sb.Clear();
                        }
                    }
                    else if (inQuotes)
                    {
                        sb.Append(c);
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        if (sb.Length > 0)
                        {
                            tokens.Add(sb.ToString());
                            sb.Clear();
                        }
                    }
                    else if (c == '{' || c == '}' || c == ',' || c == ';')
                    {
                        if (sb.Length > 0)
                        {
                            tokens.Add(sb.ToString());
                            sb.Clear();
                        }
                        tokens.Add(c.ToString());
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                }
            }

            // DoS protection: limit token list size
            if (tokens.Count > 1000000)
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Unsupported,
                    Diagnostics = new[]
                    {
                        new O3dDiagnostic
                        {
                            Severity = O3dDiagnosticSeverity.Error,
                            Code = O3dDiagnosticCode.SafetyLimitExceeded,
                            Message = "Token count exceeds safety limit."
                        }
                    }
                };
            }

            // 3. Parse tokens
            var vertices = new List<O3dVertex>();
            var triangles = new List<O3dTriangle>();
            var materialSlots = new List<O3dMaterialSlot>();
            var allDiagnostics = new List<O3dDiagnostic>();

            int index = 0;
            string NextToken() => index < tokens.Count ? tokens[index++] : string.Empty;
            string PeekToken() => index < tokens.Count ? tokens[index] : string.Empty;

            void Consume(string expected)
            {
                string actual = NextToken();
                if (actual != expected)
                {
                    throw new InvalidDataException($"Expected token '{expected}', but got '{actual}'.");
                }
            }

            void ConsumeSeparator()
            {
                while (PeekToken() == ";" || PeekToken() == ",")
                {
                    NextToken();
                }
            }

            string ReadNumberToken()
            {
                string token = NextToken();
                return token.TrimEnd(';', ',');
            }

            float ReadFloat()
            {
                string token = ReadNumberToken();
                if (float.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out float val))
                {
                    return val;
                }
                throw new InvalidDataException($"Invalid float token: '{token}'");
            }

            int ReadInt()
            {
                string token = ReadNumberToken();
                if (int.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out int val))
                {
                    return val;
                }
                throw new InvalidDataException($"Invalid integer token: '{token}'");
            }

            void SkipBlock()
            {
                Consume("{");
                int braceLevel = 1;
                while (braceLevel > 0 && index < tokens.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string t = NextToken();
                    if (t == "{") braceLevel++;
                    else if (t == "}") braceLevel--;
                }
            }

            int vertexOffset = 0;
            int materialOffset = 0;

            while (index < tokens.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string token = NextToken();
                if (token == "Mesh")
                {
                    string? meshName = null;
                    if (PeekToken() != "{")
                    {
                        meshName = NextToken();
                        if (meshName.Length > 260) meshName = meshName.Substring(0, 260);
                    }
                    Consume("{");

                    int nVertices = ReadInt();
                    ConsumeSeparator();

                    if (nVertices < 0)
                    {
                        return new O3dGeometryReadResult
                        {
                            Status = O3dGeometryStatus.Invalid,
                            Diagnostics = new[]
                            {
                                new O3dDiagnostic
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.InvalidCount,
                                    Message = $"Negative vertex count: {nVertices}"
                                }
                            }
                        };
                    }

                    if (vertexOffset + nVertices > PreviewPerformancePolicy.MaxPreviewVertices)
                    {
                        return new O3dGeometryReadResult
                        {
                            Status = O3dGeometryStatus.Unsupported,
                            Diagnostics = new[]
                            {
                                new O3dDiagnostic
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.SafetyLimitExceeded,
                                    Message = $"Vertices count exceeds MaxPreviewVertices ({PreviewPerformancePolicy.MaxPreviewVertices})."
                                }
                            }
                        };
                    }

                    var meshVertices = new List<O3dVertex>();
                    for (int i = 0; i < nVertices; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        float vx = ReadFloat();
                        ConsumeSeparator();
                        float vy = ReadFloat();
                        ConsumeSeparator();
                        float vz = ReadFloat();
                        ConsumeSeparator();

                        meshVertices.Add(new O3dVertex { X = vx, Y = vy, Z = vz, Uv = new O3dUv() });
                    }

                    int nFaces = ReadInt();
                    ConsumeSeparator();

                    if (nFaces < 0)
                    {
                        return new O3dGeometryReadResult
                        {
                            Status = O3dGeometryStatus.Invalid,
                            Diagnostics = new[]
                            {
                                new O3dDiagnostic
                                {
                                    Severity = O3dDiagnosticSeverity.Error,
                                    Code = O3dDiagnosticCode.InvalidCount,
                                    Message = $"Negative face count: {nFaces}"
                                }
                            }
                        };
                    }

                    var meshTriangles = new List<O3dTriangle>();
                    var triangleFaceMapping = new List<int>();

                    int projectedTrianglesCount = 0;

                    for (int i = 0; i < nFaces; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int nIndices = ReadInt();
                        ConsumeSeparator();

                        if (nIndices < 3)
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Invalid,
                                Diagnostics = new[]
                                {
                                    new O3dDiagnostic
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.InvalidCount,
                                        Message = $"Face {i} has fewer than 3 indices: {nIndices}"
                                    }
                                }
                            };
                        }

                        // Checked arithmetic to count projected triangles
                        checked
                        {
                            projectedTrianglesCount += (nIndices - 2);
                        }

                        if (triangles.Count + projectedTrianglesCount > PreviewPerformancePolicy.MaxPreviewTriangles)
                        {
                            return new O3dGeometryReadResult
                            {
                                Status = O3dGeometryStatus.Unsupported,
                                Diagnostics = new[]
                                {
                                    new O3dDiagnostic
                                    {
                                        Severity = O3dDiagnosticSeverity.Error,
                                        Code = O3dDiagnosticCode.SafetyLimitExceeded,
                                        Message = $"Triangles count exceeds MaxPreviewTriangles ({PreviewPerformancePolicy.MaxPreviewTriangles})."
                                    }
                                }
                            };
                        }

                        var faceIndices = new List<int>();
                        for (int j = 0; j < nIndices; j++)
                        {
                            int idx = ReadInt();
                            ConsumeSeparator();
                            faceIndices.Add(idx);
                        }
                        ConsumeSeparator();

                        for (int t = 1; t < faceIndices.Count - 1; t++)
                        {
                            int v0 = faceIndices[0];
                            int v1 = faceIndices[t];
                            int v2 = faceIndices[t + 1];

                            if (v0 < 0 || v0 >= nVertices || v1 < 0 || v1 >= nVertices || v2 < 0 || v2 >= nVertices)
                            {
                                return new O3dGeometryReadResult
                                {
                                    Status = O3dGeometryStatus.Invalid,
                                    Diagnostics = new[]
                                    {
                                        new O3dDiagnostic
                                        {
                                            Severity = O3dDiagnosticSeverity.Error,
                                            Code = O3dDiagnosticCode.InvalidIndex,
                                            Message = "Vertex index is out of bounds."
                                        }
                                    }
                                };
                            }

                            meshTriangles.Add(new O3dTriangle
                            {
                                V0 = vertexOffset + v0,
                                V1 = vertexOffset + v1,
                                V2 = vertexOffset + v2,
                                MaterialSlotIndex = materialOffset // default to first material of this mesh
                            });
                            triangleFaceMapping.Add(i);
                        }
                    }

                    vertices.AddRange(meshVertices);

                    int braceLevel = 1;
                    while (braceLevel > 0 && index < tokens.Count)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string t = NextToken();
                        if (t == "{") braceLevel++;
                        else if (t == "}") braceLevel--;
                        else if (t == "MeshTextureCoords")
                        {
                            Consume("{");
                            int nCoords = ReadInt();
                            ConsumeSeparator();

                            if (nCoords < 0)
                            {
                                return new O3dGeometryReadResult
                                {
                                    Status = O3dGeometryStatus.Invalid,
                                    Diagnostics = new[]
                                    {
                                        new O3dDiagnostic
                                        {
                                            Severity = O3dDiagnosticSeverity.Error,
                                            Code = O3dDiagnosticCode.InvalidCount,
                                            Message = $"Negative texture coordinates count: {nCoords}"
                                        }
                                    }
                                };
                            }

                            var meshUvs = new List<(float U, float V)>();
                            for (int i = 0; i < nCoords; i++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                float u = ReadFloat();
                                ConsumeSeparator();
                                float v = ReadFloat();
                                ConsumeSeparator();
                                meshUvs.Add((u, v));
                            }

                            for (int i = 0; i < Math.Min(nCoords, nVertices); i++)
                            {
                                vertices[vertexOffset + i] = vertices[vertexOffset + i] with { Uv = new O3dUv { U = meshUvs[i].U, V = meshUvs[i].V } };
                            }
                            Consume("}");
                        }
                        else if (t == "MeshNormals")
                        {
                            SkipBlock();
                        }
                        else if (t == "MeshMaterialList")
                        {
                            Consume("{");
                            int nMaterials = ReadInt();
                            ConsumeSeparator();

                            if (nMaterials < 0)
                            {
                                return new O3dGeometryReadResult
                                {
                                    Status = O3dGeometryStatus.Invalid,
                                    Diagnostics = new[]
                                    {
                                        new O3dDiagnostic
                                        {
                                            Severity = O3dDiagnosticSeverity.Error,
                                            Code = O3dDiagnosticCode.InvalidCount,
                                            Message = $"Negative materials count: {nMaterials}"
                                        }
                                    }
                                };
                            }

                            if (materialOffset + nMaterials > PreviewPerformancePolicy.MaxPreviewMaterials)
                            {
                                return new O3dGeometryReadResult
                                {
                                    Status = O3dGeometryStatus.Unsupported,
                                    Diagnostics = new[]
                                    {
                                        new O3dDiagnostic
                                        {
                                            Severity = O3dDiagnosticSeverity.Error,
                                            Code = O3dDiagnosticCode.SafetyLimitExceeded,
                                            Message = $"Materials count exceeds MaxPreviewMaterials ({PreviewPerformancePolicy.MaxPreviewMaterials})."
                                        }
                                    }
                                };
                            }

                            int nFaceMaterials = ReadInt();
                            ConsumeSeparator();

                            if (nFaceMaterials < 0)
                            {
                                return new O3dGeometryReadResult
                                {
                                    Status = O3dGeometryStatus.Invalid,
                                    Diagnostics = new[]
                                    {
                                        new O3dDiagnostic
                                        {
                                            Severity = O3dDiagnosticSeverity.Error,
                                            Code = O3dDiagnosticCode.InvalidCount,
                                            Message = $"Negative face materials count: {nFaceMaterials}"
                                        }
                                    }
                                };
                            }

                            if (nFaceMaterials != nFaces)
                            {
                                allDiagnostics.Add(new O3dDiagnostic
                                {
                                    Severity = O3dDiagnosticSeverity.Warning,
                                    Code = O3dDiagnosticCode.InvalidCount,
                                    Message = $"MeshMaterialList face materials count ({nFaceMaterials}) does not match faces count ({nFaces})."
                                });
                            }

                            var faceMaterials = new List<int>();
                            for (int i = 0; i < nFaceMaterials; i++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                int matIdx = ReadInt();
                                ConsumeSeparator();

                                if (matIdx < 0 || matIdx >= nMaterials)
                                {
                                    return new O3dGeometryReadResult
                                    {
                                        Status = O3dGeometryStatus.Invalid,
                                        Diagnostics = new[]
                                        {
                                            new O3dDiagnostic
                                            {
                                                Severity = O3dDiagnosticSeverity.Error,
                                                Code = O3dDiagnosticCode.InvalidIndex,
                                                Message = $"Face material index {matIdx} is out of bounds [0, {nMaterials - 1}]."
                                            }
                                        }
                                    };
                                }

                                faceMaterials.Add(matIdx);
                            }

                            int matListBraceLevel = 1;
                            int parsedMaterialsCount = 0;
                            while (matListBraceLevel > 0 && index < tokens.Count)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                string mToken = NextToken();
                                if (mToken == "{") matListBraceLevel++;
                                else if (mToken == "}") matListBraceLevel--;
                                else if (mToken == "Material")
                                {
                                    string? matName = null;
                                    if (PeekToken() != "{")
                                    {
                                        matName = NextToken();
                                        if (matName.Length > 260) matName = matName.Substring(0, 260);
                                    }
                                    Consume("{");

                                    string? textureRef = null;
                                    int materialBraceLevel = 1;
                                    while (materialBraceLevel > 0 && index < tokens.Count)
                                    {
                                        cancellationToken.ThrowIfCancellationRequested();

                                        string mtToken = NextToken();
                                        if (mtToken == "{") materialBraceLevel++;
                                        else if (mtToken == "}") materialBraceLevel--;
                                        else if (mtToken == "TextureFilename")
                                        {
                                            Consume("{");
                                            string fileToken = NextToken();
                                            if (fileToken.StartsWith("\"") && fileToken.EndsWith("\"") && fileToken.Length >= 2)
                                            {
                                                textureRef = fileToken.Substring(1, fileToken.Length - 2);
                                            }
                                            else
                                            {
                                                textureRef = fileToken;
                                            }
                                            if (textureRef.Length > 260) textureRef = textureRef.Substring(0, 260);

                                            ConsumeSeparator();
                                            Consume("}");
                                            ConsumeSeparator();
                                        }
                                    }

                                    materialSlots.Add(new O3dMaterialSlot
                                    {
                                        MaterialName = matName ?? $"Material_{materialOffset + parsedMaterialsCount}",
                                        TextureReference = textureRef
                                    });
                                    parsedMaterialsCount++;
                                }
                            }

                            for (int i = 0; i < meshTriangles.Count; i++)
                            {
                                int faceIdx = triangleFaceMapping[i];
                                if (faceIdx >= 0 && faceIdx < faceMaterials.Count)
                                {
                                    int localMatIdx = faceMaterials[faceIdx];
                                    if (localMatIdx >= 0 && localMatIdx < nMaterials)
                                    {
                                        meshTriangles[i] = meshTriangles[i] with { MaterialSlotIndex = materialOffset + localMatIdx };
                                    }
                                }
                            }

                            materialOffset += nMaterials;
                        }
                    }

                    triangles.AddRange(meshTriangles);
                    vertexOffset += nVertices;
                }
            }

            if (vertices.Count == 0)
            {
                return new O3dGeometryReadResult
                {
                    Status = O3dGeometryStatus.Invalid,
                    Diagnostics = new[]
                    {
                        new O3dDiagnostic
                        {
                            Severity = O3dDiagnosticSeverity.Error,
                            Code = O3dDiagnosticCode.TruncatedStream,
                            Message = "No valid geometry meshes were parsed from the DirectX .x file."
                        }
                    }
                };
            }

            return new O3dGeometryReadResult
            {
                Status = O3dGeometryStatus.Success,
                MeshData = new O3dMeshData
                {
                    Vertices = vertices,
                    Triangles = triangles,
                    MaterialSlots = materialSlots
                },
                Diagnostics = allDiagnostics
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OverflowException)
        {
            return new O3dGeometryReadResult
            {
                Status = O3dGeometryStatus.Unsupported,
                Diagnostics = new[]
                {
                    new O3dDiagnostic
                    {
                        Severity = O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.SafetyLimitExceeded,
                        Message = "Integer overflow detected during triangle count calculation."
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new O3dGeometryReadResult
            {
                Status = O3dGeometryStatus.Invalid,
                Diagnostics = new[]
                {
                    new O3dDiagnostic
                    {
                        Severity = O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.TruncatedStream,
                        Message = $"DirectX .x parsing failed: {ex.Message}"
                    }
                }
            };
        }
    }
}
