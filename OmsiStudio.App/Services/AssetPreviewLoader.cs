using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.App.Services;

/// <summary>
/// A concrete implementation of <see cref="IAssetPreviewLoader"/> that parses O3D geometry data 
/// using <see cref="IO3dGeometryReader"/> and computes bounds using <see cref="IMeshBoundsCalculator"/>.
/// </summary>
public class AssetPreviewLoader : IAssetPreviewLoader
{
    private readonly IO3dGeometryReader _geometryReader;
    private readonly IMeshBoundsCalculator _boundsCalculator;
    private readonly ILocalizationService? _localizationService;
    private readonly IDirectXGeometryReader _directXReader;

    private int? _maxPreviewVertices;
    private int? _maxPreviewTriangles;
    private int? _maxPreviewMaterials;

    public int MaxPreviewVertices
    {
        get => _maxPreviewVertices ?? PreviewPerformancePolicy.MaxPreviewVertices;
        set => _maxPreviewVertices = value;
    }

    public int MaxPreviewTriangles
    {
        get => _maxPreviewTriangles ?? PreviewPerformancePolicy.MaxPreviewTriangles;
        set => _maxPreviewTriangles = value;
    }

    public int MaxPreviewMaterials
    {
        get => _maxPreviewMaterials ?? PreviewPerformancePolicy.MaxPreviewMaterials;
        set => _maxPreviewMaterials = value;
    }

    public AssetPreviewLoader(
        IO3dGeometryReader geometryReader,
        IMeshBoundsCalculator boundsCalculator,
        ILocalizationService? localizationService = null,
        IDirectXGeometryReader? directXReader = null)
    {
        _geometryReader = geometryReader ?? throw new ArgumentNullException(nameof(geometryReader));
        _boundsCalculator = boundsCalculator ?? throw new ArgumentNullException(nameof(boundsCalculator));
        _localizationService = localizationService;
        _directXReader = directXReader ?? new DirectXGeometryReader();
    }

    /// <inheritdoc />
    public async Task<AssetPreviewResult> LoadAsync(AssetPreviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Gather all paths to load alongside their transforms
        var pathsToLoad = new List<(string Path, OmsiMeshTransform Transform)>();
        if (request.ModelReferences != null && request.ModelReferences.Count > 0)
        {
            foreach (var r in request.ModelReferences)
            {
                if (r != null && !string.IsNullOrWhiteSpace(r.ResolvedPath))
                {
                    pathsToLoad.Add((r.ResolvedPath, r.Transform ?? OmsiMeshTransform.Identity));
                }
            }
        }
        else if (request.ModelPaths != null && request.ModelPaths.Count > 0)
        {
            foreach (var p in request.ModelPaths)
            {
                if (!string.IsNullOrWhiteSpace(p))
                {
                    pathsToLoad.Add((p, OmsiMeshTransform.Identity));
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.ModelPath))
        {
            pathsToLoad.Add((request.ModelPath, OmsiMeshTransform.Identity));
        }

        if (pathsToLoad.Count == 0)
        {
            return new AssetPreviewResult
            {
                Status = AssetPreviewStatus.Invalid,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Code = O3dDiagnosticCode.InvalidPath, Message = "Model path is empty." }]
            };
        }

        var allDiagnostics = new List<O3dDiagnostic>();
        var loadedMeshes = new List<(O3dMeshData Mesh, string Path, OmsiMeshTransform Transform)>();

        if (request.ModelReferences != null)
        {
            foreach (var r in request.ModelReferences)
            {
                if (r != null && r.TransformWarnings != null)
                {
                    foreach (var w in r.TransformWarnings)
                    {
                        allDiagnostics.Add(new O3dDiagnostic
                        {
                            Severity = O3dDiagnosticSeverity.Warning,
                            Code = O3dDiagnosticCode.InvalidPath,
                            Message = $"[{Path.GetFileName(r.MeshPath)}] {w}"
                        });
                    }
                }
            }
        }

        bool hasFileNotFound = false;
        bool hasUnsupported = false;
        bool hasReadFailed = false;

        try
        {
            foreach (var item in pathsToLoad)
            {
                var path = item.Path;
                var transform = item.Transform;
                if (string.IsNullOrWhiteSpace(path))
                {
                    allDiagnostics.Add(new O3dDiagnostic
                    {
                        Severity = pathsToLoad.Count > 1 ? O3dDiagnosticSeverity.Warning : O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.InvalidPath,
                        Message = "Model path is empty."
                    });
                    continue;
                }

                // 1. Detect format and verify it is not Unsupported
                var format = OmsiMeshFormatHelper.DetectFormat(path);
                if (format == OmsiMeshFormat.Unsupported)
                {
                    hasUnsupported = true;
                    string msg = pathsToLoad.Count > 1
                        ? $"File type of {path} is not supported. Only .o3d files are supported."
                        : "File type is not supported. Only .o3d files are supported.";
                    allDiagnostics.Add(new O3dDiagnostic
                    {
                        Severity = pathsToLoad.Count > 1 ? O3dDiagnosticSeverity.Warning : O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.UnsupportedFormat,
                        Message = msg
                    });
                    continue;
                }

                // 2. Verify file existence
                if (!File.Exists(path))
                {
                    hasFileNotFound = true;
                    allDiagnostics.Add(new O3dDiagnostic
                    {
                        Severity = pathsToLoad.Count > 1 ? O3dDiagnosticSeverity.Warning : O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.FileNotFound,
                        Message = pathsToLoad.Count > 1 ? $"File not found: {path}" : $"File not found: {request.ModelPath}"
                    });
                    continue;
                }

                try
                {
                    // 3. Read geometry using appropriate format reader
                    O3dGeometryReadResult readResult;
                    if (format == OmsiMeshFormat.O3d)
                    {
                        readResult = await _geometryReader.ReadAsync(path, cancellationToken);
                    }
                    else
                    {
                        readResult = await _directXReader.ReadAsync(path, cancellationToken);
                    }
                    cancellationToken.ThrowIfCancellationRequested();

                    if (readResult.Diagnostics != null)
                    {
                        foreach (var diag in readResult.Diagnostics)
                        {
                            var severity = pathsToLoad.Count > 1 ? O3dDiagnosticSeverity.Warning : diag.Severity;
                            string message = diag.Message;
                            if (format == OmsiMeshFormat.DirectX && _localizationService != null && diag.Code == O3dDiagnosticCode.UnsupportedFormat && diag.Message.Contains("DirectX .x"))
                            {
                                message = _localizationService["DirectXMeshParserPending"];
                            }

                            allDiagnostics.Add(new O3dDiagnostic
                            {
                                Severity = severity,
                                Code = diag.Code,
                                Message = pathsToLoad.Count > 1 ? $"[{Path.GetFileName(path)}] {message}" : message
                            });
                        }
                    }

                    if (readResult.Status == O3dGeometryStatus.Success && readResult.MeshData != null)
                    {
                        loadedMeshes.Add((readResult.MeshData, path, transform));
                    }
                    else
                    {
                        var code = O3dDiagnosticCode.UnsupportedFormat;
                        if (readResult.Status == O3dGeometryStatus.Unsupported || readResult.Status == O3dGeometryStatus.Encrypted)
                        {
                            hasUnsupported = true;
                            code = readResult.Status == O3dGeometryStatus.Encrypted ? O3dDiagnosticCode.EncryptedFile : O3dDiagnosticCode.UnsupportedFormat;
                        }
                        else
                        {
                            hasReadFailed = true;
                            code = O3dDiagnosticCode.ReadFailed;
                        }

                        if (readResult.Diagnostics == null || readResult.Diagnostics.Count == 0)
                        {
                            allDiagnostics.Add(new O3dDiagnostic
                            {
                                Severity = pathsToLoad.Count > 1 ? O3dDiagnosticSeverity.Warning : O3dDiagnosticSeverity.Error,
                                Code = code,
                                Message = $"Failed to load mesh reference {path}: Status is {readResult.Status}."
                            });
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    hasReadFailed = true;
                    allDiagnostics.Add(new O3dDiagnostic
                    {
                        Severity = pathsToLoad.Count > 1 ? O3dDiagnosticSeverity.Warning : O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.ReadFailed,
                        Message = $"Failed to load O3D file {path}: {ex.Message}"
                    });
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (loadedMeshes.Count > 0)
            {
                O3dMeshData combinedMesh;
                if (pathsToLoad.Count == 1 && !request.ApplyModelTransforms)
                {
                    combinedMesh = loadedMeshes[0].Mesh;
                }
                else
                {
                    // Combine meshes
                    var combinedVertices = new List<O3dVertex>();
                    var combinedTriangles = new List<O3dTriangle>();
                    var combinedMaterialSlots = new List<O3dMaterialSlot>();

                    int vertexOffset = 0;
                    int materialOffset = 0;

                    foreach (var tuple in loadedMeshes)
                    {
                        var mesh = tuple.Mesh;
                        var sourcePath = tuple.Path;
                        var transform = tuple.Transform;
                        if (mesh.Vertices != null)
                        {
                            foreach (var v in mesh.Vertices)
                            {
                                combinedVertices.Add(TransformVertex(v, transform));
                            }
                        }
                        if (mesh.MaterialSlots != null)
                        {
                            foreach (var slot in mesh.MaterialSlots)
                            {
                                combinedMaterialSlots.Add(slot with { SourceModelPath = sourcePath });
                            }
                        }
                        if (mesh.Triangles != null)
                        {
                            foreach (var tri in mesh.Triangles)
                            {
                                combinedTriangles.Add(new O3dTriangle
                                {
                                    V0 = tri.V0 + vertexOffset,
                                    V1 = tri.V1 + vertexOffset,
                                    V2 = tri.V2 + vertexOffset,
                                    MaterialSlotIndex = tri.MaterialSlotIndex >= 0 ? (tri.MaterialSlotIndex + materialOffset) : -1
                                });
                            }
                        }

                        vertexOffset += mesh.Vertices?.Count ?? 0;
                        materialOffset += mesh.MaterialSlots?.Count ?? 0;
                    }

                    combinedMesh = new O3dMeshData
                    {
                        Vertices = combinedVertices,
                        Triangles = combinedTriangles,
                        MaterialSlots = combinedMaterialSlots
                    };
                }

                int combinedVertexCount = combinedMesh.Vertices.Count;
                int combinedTriangleCount = combinedMesh.Triangles.Count;
                int combinedMaterialCount = combinedMesh.MaterialSlots.Count;

                if (combinedVertexCount > MaxPreviewVertices || combinedTriangleCount > MaxPreviewTriangles || combinedMaterialCount > MaxPreviewMaterials)
                {
                    string meshPrefix = pathsToLoad.Count > 1 ? "combined mesh" : "mesh";
                    string msg = $"Preview skipped because {meshPrefix} is too large: Vertices={combinedVertexCount} (Max={MaxPreviewVertices}), Triangles={combinedTriangleCount} (Max={MaxPreviewTriangles}), Materials={combinedMaterialCount} (Max={MaxPreviewMaterials})";
                    if (_localizationService != null)
                    {
                        msg = string.Format(_localizationService["PreviewMeshTooLarge"], combinedVertexCount, MaxPreviewVertices, combinedTriangleCount, MaxPreviewTriangles, combinedMaterialCount, MaxPreviewMaterials);
                    }

                    allDiagnostics.Add(new O3dDiagnostic
                    {
                        Severity = pathsToLoad.Count > 1 ? O3dDiagnosticSeverity.Warning : O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.SafetyLimitExceeded,
                        Message = msg
                    });

                    return new AssetPreviewResult
                    {
                        Status = AssetPreviewStatus.Unsupported,
                        MeshData = null,
                        Bounds = null,
                        Diagnostics = allDiagnostics
                    };
                }

                var bounds = _boundsCalculator.CalculateBounds(combinedMesh);

                return new AssetPreviewResult
                {
                    Status = pathsToLoad.Count == 1 ? AssetPreviewStatus.Success : AssetPreviewStatus.Success,
                    MeshData = combinedMesh,
                    Bounds = bounds,
                    Diagnostics = allDiagnostics
                };
            }
            else
            {
                AssetPreviewStatus status = AssetPreviewStatus.Failed;
                if (hasReadFailed)
                {
                    status = AssetPreviewStatus.Failed;
                }
                else if (hasFileNotFound)
                {
                    status = AssetPreviewStatus.Missing;
                }
                else if (hasUnsupported)
                {
                    status = AssetPreviewStatus.Unsupported;
                }
                else
                {
                    status = AssetPreviewStatus.Invalid;
                }

                return new AssetPreviewResult
                {
                    Status = status,
                    MeshData = null,
                    Bounds = null,
                    Diagnostics = allDiagnostics
                };
            }
        }
        catch (OperationCanceledException)
        {
            return new AssetPreviewResult
            {
                Status = AssetPreviewStatus.Cancelled,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Warning, Code = O3dDiagnosticCode.LoadCancelled, Message = "Preview loading cancelled." }]
            };
        }
        catch (Exception ex)
        {
            return new AssetPreviewResult
            {
                Status = AssetPreviewStatus.Failed,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Code = O3dDiagnosticCode.ReadFailed, Message = $"Failed to load O3D file: {ex.Message}" }]
            };
        }
    }

    private static AssetPreviewStatus MapStatus(O3dGeometryStatus status)
    {
        return status switch
        {
            O3dGeometryStatus.Success => AssetPreviewStatus.Success,
            O3dGeometryStatus.Unsupported => AssetPreviewStatus.Unsupported,
            O3dGeometryStatus.Encrypted => AssetPreviewStatus.Encrypted,
            O3dGeometryStatus.Invalid => AssetPreviewStatus.Invalid,
            O3dGeometryStatus.Failed => AssetPreviewStatus.Failed,
            _ => AssetPreviewStatus.Failed
        };
    }

    private static O3dVertex TransformVertex(O3dVertex v, OmsiMeshTransform t)
    {
        double x = v.X * t.ScaleX;
        double y = v.Y * t.ScaleY;
        double z = v.Z * t.ScaleZ;

        // Rotate around Y axis (roll)
        if (t.RotY != 0.0)
        {
            double rad = t.RotY * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double newX = x * cos + z * sin;
            double newZ = -x * sin + z * cos;
            x = newX;
            z = newZ;
        }

        // Rotate around X axis (pitch)
        if (t.RotX != 0.0)
        {
            double rad = t.RotX * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double newY = y * cos - z * sin;
            double newZ = y * sin + z * cos;
            y = newY;
            z = newZ;
        }

        // Rotate around Z axis (yaw)
        if (t.RotZ != 0.0)
        {
            double rad = t.RotZ * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double newX = x * cos - y * sin;
            double newY = x * sin + y * cos;
            x = newX;
            y = newY;
        }

        // Translate
        x += t.PosX;
        y += t.PosY;
        z += t.PosZ;

        return v with
        {
            X = (float)x,
            Y = (float)y,
            Z = (float)z
        };
    }
}
