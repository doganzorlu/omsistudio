using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using OmsiStudio.App.Services;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.App.Tests;

public class AssetPreviewLoaderTests : IDisposable
{
    private readonly string _tempFile;

    public AssetPreviewLoaderTests()
    {
        _tempFile = Path.GetTempFileName() + ".o3d";
        File.WriteAllText(_tempFile, "dummy o3d content");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [Fact]
    public async Task LoadAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = new AssetPreviewLoader(new FakeO3dGeometryReader(), new FakeMeshBoundsCalculator());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.LoadAsync(null!));
    }

    [Fact]
    public async Task LoadAsync_WithEmptyPath_ReturnsInvalidResult()
    {
        // Arrange
        var loader = new AssetPreviewLoader(new FakeO3dGeometryReader(), new FakeMeshBoundsCalculator());
        var request = new AssetPreviewRequest { ModelPath = "" };

        // Act
        var result = await loader.LoadAsync(request);

        // Assert
        Assert.Equal(AssetPreviewStatus.Invalid, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.InvalidPath, result.Diagnostics[0].Code);
        Assert.Contains("Model path is empty", result.Diagnostics[0].Message);
    }

    [Fact]
    public async Task LoadAsync_WithNonExistentFile_ReturnsMissingResult()
    {
        // Arrange
        var loader = new AssetPreviewLoader(new FakeO3dGeometryReader(), new FakeMeshBoundsCalculator());
        var request = new AssetPreviewRequest { ModelPath = "non_existent_file.o3d" };

        // Act
        var result = await loader.LoadAsync(request);

        // Assert
        Assert.Equal(AssetPreviewStatus.Missing, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.FileNotFound, result.Diagnostics[0].Code);
        Assert.Contains("File not found", result.Diagnostics[0].Message);
    }

    [Fact]
    public async Task LoadAsync_WithUnsupportedExtension_ReturnsUnsupportedResult()
    {
        // Arrange
        var loader = new AssetPreviewLoader(new FakeO3dGeometryReader(), new FakeMeshBoundsCalculator());
        var request = new AssetPreviewRequest { ModelPath = "model.txt" };

        // Act
        var result = await loader.LoadAsync(request);

        // Assert
        Assert.Equal(AssetPreviewStatus.Unsupported, result.Status);
        Assert.Null(result.MeshData);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.UnsupportedFormat, result.Diagnostics[0].Code);
        Assert.Contains("File type is not supported", result.Diagnostics[0].Message);
    }

    [Fact]
    public async Task LoadAsync_WhenSuccess_ReturnsSuccessResultAndComputesBounds()
    {
        // Arrange
        var meshData = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() { X = 1f, Y = 2f, Z = 3f } }
        };
        var expectedBounds = new MeshBounds
        {
            Min = new PreviewVector3D { X = 1f, Y = 2f, Z = 3f },
            Max = new PreviewVector3D { X = 1f, Y = 2f, Z = 3f }
        };

        var fakeReader = new FakeO3dGeometryReader
        {
            Result = new O3dGeometryReadResult
            {
                Status = O3dGeometryStatus.Success,
                MeshData = meshData,
                Diagnostics = []
            }
        };
        var fakeBoundsCalc = new FakeMeshBoundsCalculator { BoundsResult = expectedBounds };
        var loader = new AssetPreviewLoader(fakeReader, fakeBoundsCalc);
        var request = new AssetPreviewRequest { ModelPath = _tempFile };

        // Act
        var result = await loader.LoadAsync(request);

        // Assert
        Assert.Equal(AssetPreviewStatus.Success, result.Status);
        Assert.Equal(meshData, result.MeshData);
        Assert.Equal(expectedBounds, result.Bounds);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task LoadAsync_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var fakeReader = new FakeO3dGeometryReader
        {
            Result = new O3dGeometryReadResult { Status = O3dGeometryStatus.Success }
        };
        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());
        var request = new AssetPreviewRequest { ModelPath = _tempFile };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await loader.LoadAsync(request, cts.Token);

        // Assert
        Assert.Equal(AssetPreviewStatus.Cancelled, result.Status);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.LoadCancelled, result.Diagnostics[0].Code);
        Assert.Contains("cancelled", result.Diagnostics[0].Message);
    }

    [Fact]
    public async Task LoadAsync_WhenExceptionThrown_ReturnsFailedResult()
    {
        // Arrange
        var fakeReader = new FakeO3dGeometryReader
        {
            ReadFunc = (path, token) => throw new InvalidOperationException("Reader crashed.")
        };
        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());
        var request = new AssetPreviewRequest { ModelPath = _tempFile };

        // Act
        var result = await loader.LoadAsync(request);

        // Assert
        Assert.Equal(AssetPreviewStatus.Failed, result.Status);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.ReadFailed, result.Diagnostics[0].Code);
        Assert.Contains("Reader crashed", result.Diagnostics[0].Message);
    }

    private class FakeO3dGeometryReader : IO3dGeometryReader
    {
        public O3dGeometryReadResult Result { get; set; } = new() { Status = O3dGeometryStatus.Failed };
        public Func<string, CancellationToken, O3dGeometryReadResult>? ReadFunc { get; set; }

        public Task<O3dGeometryReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<O3dGeometryReadResult>(cancellationToken);
            }
            if (ReadFunc != null)
            {
                try
                {
                    return Task.FromResult(ReadFunc(filePath, cancellationToken));
                }
                catch (Exception ex)
                {
                    return Task.FromException<O3dGeometryReadResult>(ex);
                }
            }
            return Task.FromResult(Result);
        }
    }

    [Fact]
    public async Task LoadAsync_UnderLimits_ReturnsSuccess()
    {
        // Arrange
        var meshData = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new(), new() },
            Triangles = new List<O3dTriangle> { new() },
            MaterialSlots = new List<O3dMaterialSlot> { new() }
        };
        var fakeReader = new FakeO3dGeometryReader
        {
            Result = new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = meshData }
        };
        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());
        loader.MaxPreviewVertices = 5;
        loader.MaxPreviewTriangles = 5;
        loader.MaxPreviewMaterials = 5;
        var request = new AssetPreviewRequest { ModelPath = _tempFile };

        // Act
        var result = await loader.LoadAsync(request);

        // Assert
        Assert.Equal(AssetPreviewStatus.Success, result.Status);
        Assert.NotNull(result.MeshData);
    }

    [Fact]
    public async Task LoadAsync_ExceedsVertexLimit_SkipsPreview()
    {
        // Arrange
        var meshData = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new(), new(), new() },
            Triangles = new List<O3dTriangle> { new() },
            MaterialSlots = new List<O3dMaterialSlot> { new() }
        };
        var fakeReader = new FakeO3dGeometryReader
        {
            Result = new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = meshData }
        };
        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());
        loader.MaxPreviewVertices = 2; // Limit is 2, we have 3
        loader.MaxPreviewTriangles = 5;
        loader.MaxPreviewMaterials = 5;
        var request = new AssetPreviewRequest { ModelPath = _tempFile };

        // Act
        var result = await loader.LoadAsync(request);

        // Assert
        Assert.Equal(AssetPreviewStatus.Unsupported, result.Status);
        Assert.Null(result.MeshData);
        Assert.Null(result.Bounds);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, result.Diagnostics[0].Code);
        Assert.Contains("Preview skipped because mesh is too large", result.Diagnostics[0].Message);
    }

    [Fact]
    public async Task LoadAsync_ExceedsTriangleLimit_SkipsPreview()
    {
        // Arrange
        var meshData = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new(), new() },
            Triangles = new List<O3dTriangle> { new(), new(), new() },
            MaterialSlots = new List<O3dMaterialSlot> { new() }
        };
        var fakeReader = new FakeO3dGeometryReader
        {
            Result = new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = meshData }
        };
        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());
        loader.MaxPreviewVertices = 5;
        loader.MaxPreviewTriangles = 2; // Limit is 2, we have 3
        loader.MaxPreviewMaterials = 5;
        var request = new AssetPreviewRequest { ModelPath = _tempFile };

        // Act
        var result = await loader.LoadAsync(request);

        // Assert
        Assert.Equal(AssetPreviewStatus.Unsupported, result.Status);
        Assert.Null(result.MeshData);
        Assert.Null(result.Bounds);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, result.Diagnostics[0].Code);
        Assert.Contains("Preview skipped because mesh is too large", result.Diagnostics[0].Message);
    }

    [Fact]
    public async Task LoadAsync_ExceedsMaterialLimit_SkipsPreview()
    {
        // Arrange
        var meshData = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new(), new() },
            Triangles = new List<O3dTriangle> { new() },
            MaterialSlots = new List<O3dMaterialSlot> { new(), new(), new() }
        };
        var fakeReader = new FakeO3dGeometryReader
        {
            Result = new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = meshData }
        };
        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());
        loader.MaxPreviewVertices = 5;
        loader.MaxPreviewTriangles = 5;
        loader.MaxPreviewMaterials = 2; // Limit is 2, we have 3
        var request = new AssetPreviewRequest { ModelPath = _tempFile };

        // Act
        var result = await loader.LoadAsync(request);

        // Assert
        Assert.Equal(AssetPreviewStatus.Unsupported, result.Status);
        Assert.Null(result.MeshData);
        Assert.Null(result.Bounds);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, result.Diagnostics[0].Code);
        Assert.Contains("Preview skipped because mesh is too large", result.Diagnostics[0].Message);
    }

    [Fact]
    public async Task LoadAsync_ExceedsLimits_WithLocalizationService_ReturnsLocalizedMessage()
    {
        // Arrange
        var meshData = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new(), new(), new() }
        };
        var fakeReader = new FakeO3dGeometryReader
        {
            Result = new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = meshData }
        };
        
        var locService = new LocalizationService();
        var request = new AssetPreviewRequest { ModelPath = _tempFile };

        // 1. TR Localization check
        locService.SetCulture("tr-TR");
        var loaderTr = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator(), locService);
        loaderTr.MaxPreviewVertices = 2; // trigger violation

        // Act TR
        var resultTr = await loaderTr.LoadAsync(request);

        // Assert TR
        Assert.Equal(AssetPreviewStatus.Unsupported, resultTr.Status);
        Assert.Null(resultTr.Bounds);
        Assert.Contains("Mesh çok büyük olduğu için önizleme atlandı", resultTr.Diagnostics[0].Message);

        // 2. EN Localization check
        locService.SetCulture("en-US");
        var loaderEn = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator(), locService);
        loaderEn.MaxPreviewVertices = 2; // trigger violation

        // Act EN
        var resultEn = await loaderEn.LoadAsync(request);

        // Assert EN
        Assert.Equal(AssetPreviewStatus.Unsupported, resultEn.Status);
        Assert.Null(resultEn.Bounds);
        Assert.Contains("Preview skipped because mesh is too large", resultEn.Diagnostics[0].Message);
    }

    private class FakeMeshBoundsCalculator : IMeshBoundsCalculator
    {
        public MeshBounds BoundsResult { get; set; } = new();

        public MeshBounds CalculateBounds(O3dMeshData meshData)
        {
            return BoundsResult;
        }
    }
}
