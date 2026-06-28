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

    public int MaxPreviewVertices { get; set; } = 100000;
    public int MaxPreviewTriangles { get; set; } = 100000;
    public int MaxPreviewMaterials { get; set; } = 100;

    public AssetPreviewLoader(IO3dGeometryReader geometryReader, IMeshBoundsCalculator boundsCalculator, ILocalizationService? localizationService = null)
    {
        _geometryReader = geometryReader ?? throw new ArgumentNullException(nameof(geometryReader));
        _boundsCalculator = boundsCalculator ?? throw new ArgumentNullException(nameof(boundsCalculator));
        _localizationService = localizationService;
    }

    /// <inheritdoc />
    public async Task<AssetPreviewResult> LoadAsync(AssetPreviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ModelPath))
        {
            return new AssetPreviewResult
            {
                Status = AssetPreviewStatus.Invalid,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Code = O3dDiagnosticCode.InvalidPath, Message = "Model path is empty." }]
            };
        }

        // 1. Verify file extension is .o3d
        string extension = Path.GetExtension(request.ModelPath);
        if (!extension.Equals(".o3d", StringComparison.OrdinalIgnoreCase))
        {
            return new AssetPreviewResult
            {
                Status = AssetPreviewStatus.Unsupported,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Code = O3dDiagnosticCode.UnsupportedFormat, Message = "File type is not supported. Only .o3d files are supported." }]
            };
        }

        // 2. Verify file existence
        if (!File.Exists(request.ModelPath))
        {
            return new AssetPreviewResult
            {
                Status = AssetPreviewStatus.Missing,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Code = O3dDiagnosticCode.FileNotFound, Message = $"File not found: {request.ModelPath}" }]
            };
        }

        try
        {
            // 3. Read geometry using IO3dGeometryReader
            var readResult = await _geometryReader.ReadAsync(request.ModelPath, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var status = MapStatus(readResult.Status);
            MeshBounds? bounds = null;

            if (status == AssetPreviewStatus.Success && readResult.MeshData != null)
            {
                var mesh = readResult.MeshData;
                int vertexCount = mesh.Vertices?.Count ?? 0;
                int triangleCount = mesh.Triangles?.Count ?? 0;
                int materialCount = mesh.MaterialSlots?.Count ?? 0;

                if (vertexCount > MaxPreviewVertices || triangleCount > MaxPreviewTriangles || materialCount > MaxPreviewMaterials)
                {
                    string msg = $"Preview skipped because mesh is too large: Vertices={vertexCount} (Max={MaxPreviewVertices}), Triangles={triangleCount} (Max={MaxPreviewTriangles}), Materials={materialCount} (Max={MaxPreviewMaterials})";
                    if (_localizationService != null)
                    {
                        msg = string.Format(_localizationService["PreviewMeshTooLarge"], vertexCount, MaxPreviewVertices, triangleCount, MaxPreviewTriangles, materialCount, MaxPreviewMaterials);
                    }

                    var newDiags = new List<O3dDiagnostic>(readResult.Diagnostics);
                    newDiags.Add(new O3dDiagnostic
                    {
                        Severity = O3dDiagnosticSeverity.Warning,
                        Code = O3dDiagnosticCode.SafetyLimitExceeded,
                        Message = msg
                    });

                    return new AssetPreviewResult
                    {
                        Status = AssetPreviewStatus.Unsupported,
                        MeshData = null,
                        Bounds = null,
                        Diagnostics = newDiags
                    };
                }

                // 4. Calculate bounds
                bounds = _boundsCalculator.CalculateBounds(readResult.MeshData);
            }

            return new AssetPreviewResult
            {
                Status = status,
                MeshData = readResult.MeshData,
                Bounds = bounds,
                Diagnostics = readResult.Diagnostics
            };
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
}
