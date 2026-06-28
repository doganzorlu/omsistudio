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

    [Fact]
    public async Task LoadAsync_MultipleMeshes_CombinesAndOffsetsCorrectly()
    {
        // Arrange
        var mesh1 = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() { X = 1 }, new() { X = 2 } },
            Triangles = new List<O3dTriangle> { new() { V0 = 0, V1 = 1, V2 = 0, MaterialSlotIndex = 0 } },
            MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "Mat1" } }
        };

        var mesh2 = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() { X = 3 }, new() { X = 4 } },
            Triangles = new List<O3dTriangle> { new() { V0 = 0, V1 = 1, V2 = 0, MaterialSlotIndex = 0 } },
            MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "Mat2" } }
        };

        var fakeReader = new FakeO3dGeometryReader();
        fakeReader.ReadFunc = (path, ct) =>
        {
            if (path.Contains("mesh1"))
                return new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh1 };
            return new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh2 };
        };

        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());
        var request = new AssetPreviewRequest
        {
            ModelPaths = new[] { "mesh1.o3d", "mesh2.o3d" }
        };

        // Create dummy physical files so Exists check passes
        File.WriteAllText("mesh1.o3d", "");
        File.WriteAllText("mesh2.o3d", "");

        try
        {
            // Act
            var result = await loader.LoadAsync(request);

            // Assert
            Assert.Equal(AssetPreviewStatus.Success, result.Status);
            var combined = result.MeshData;
            Assert.NotNull(combined);

            // Combined Vertex Count: 2 + 2 = 4
            Assert.Equal(4, combined.Vertices.Count);
            Assert.Equal(1f, combined.Vertices[0].X);
            Assert.Equal(4f, combined.Vertices[3].X);

            // Combined Material Slots Count: 1 + 1 = 2
            Assert.Equal(2, combined.MaterialSlots.Count);
            Assert.Equal("Mat1", combined.MaterialSlots[0].MaterialName);
            Assert.Equal("Mat2", combined.MaterialSlots[1].MaterialName);

            // Triangles should have offset vertex indices and material slot indices
            Assert.Equal(2, combined.Triangles.Count);
            
            // First Triangle (from mesh1): V0=0, V1=1, V2=0, MatIndex=0
            var t0 = combined.Triangles[0];
            Assert.Equal(0, t0.V0);
            Assert.Equal(1, t0.V1);
            Assert.Equal(0, t0.MaterialSlotIndex);

            // Second Triangle (from mesh2): offset by vertices=2, materials=1 -> V0=2, V1=3, V2=2, MatIndex=1
            var t1 = combined.Triangles[1];
            Assert.Equal(2, t1.V0);
            Assert.Equal(3, t1.V1);
            Assert.Equal(1, t1.MaterialSlotIndex);
        }
        finally
        {
            if (File.Exists("mesh1.o3d")) File.Delete("mesh1.o3d");
            if (File.Exists("mesh2.o3d")) File.Delete("mesh2.o3d");
        }
    }

    [Fact]
    public async Task LoadAsync_OneSuccessOneFailure_ReturnsSuccessWithDiagnostics()
    {
        // Arrange
        var mesh1 = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() },
            Triangles = new List<O3dTriangle> { new() },
            MaterialSlots = new List<O3dMaterialSlot> { new() }
        };

        var fakeReader = new FakeO3dGeometryReader();
        fakeReader.ReadFunc = (path, ct) =>
        {
            if (path.Contains("mesh1"))
                return new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh1 };
            return new O3dGeometryReadResult { Status = O3dGeometryStatus.Unsupported };
        };

        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());
        var request = new AssetPreviewRequest
        {
            ModelPaths = new[] { "mesh1.o3d", "failed.o3d" }
        };

        File.WriteAllText("mesh1.o3d", "");
        File.WriteAllText("failed.o3d", "");

        try
        {
            // Act
            var result = await loader.LoadAsync(request);

            // Assert
            Assert.Equal(AssetPreviewStatus.Success, result.Status);
            Assert.NotNull(result.MeshData);
            Assert.Single(result.MeshData.Vertices);
            
            // There should be a warning diagnostic indicating failure
            Assert.Contains(result.Diagnostics, d => d.Severity == O3dDiagnosticSeverity.Warning);
        }
        finally
        {
            if (File.Exists("mesh1.o3d")) File.Delete("mesh1.o3d");
            if (File.Exists("failed.o3d")) File.Delete("failed.o3d");
        }
    }

    [Fact]
    public async Task LoadAsync_AllFailures_ReturnsControlledFailureStatus()
    {
        // Arrange
        var fakeReader = new FakeO3dGeometryReader();
        fakeReader.ReadFunc = (path, ct) => new O3dGeometryReadResult { Status = O3dGeometryStatus.Failed };

        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());
        var request = new AssetPreviewRequest
        {
            ModelPaths = new[] { "mesh1.o3d", "mesh2.o3d" }
        };

        File.WriteAllText("mesh1.o3d", "");
        File.WriteAllText("mesh2.o3d", "");

        try
        {
            // Act
            var result = await loader.LoadAsync(request);

            // Assert
            Assert.NotEqual(AssetPreviewStatus.Success, result.Status);
            Assert.Null(result.MeshData);
        }
        finally
        {
            if (File.Exists("mesh1.o3d")) File.Delete("mesh1.o3d");
            if (File.Exists("mesh2.o3d")) File.Delete("mesh2.o3d");
        }
    }

    [Fact]
    public async Task LoadAsync_BinaryDirectXMesh_ReturnsUnsupportedWithBinaryNotSupportedDiagnostic()
    {
        // Arrange
        var fakeReader = new FakeO3dGeometryReader();
        var locService = new LocalizationService();
        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator(), locService);
        var request = new AssetPreviewRequest { ModelPath = "model.x" };

        byte[] headerBytes = new byte[16];
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("xof 0303bin 0064"), headerBytes, 16);
        File.WriteAllBytes("model.x", headerBytes);

        try
        {
            // Act TR
            locService.SetCulture("tr-TR");
            var resultTr = await loader.LoadAsync(request);

            // Assert TR
            Assert.Equal(AssetPreviewStatus.Unsupported, resultTr.Status);
            Assert.Single(resultTr.Diagnostics);
            Assert.Equal(O3dDiagnosticCode.UnsupportedFormat, resultTr.Diagnostics[0].Code);
            Assert.Contains("DirectX .x mesh formatı tanındı ancak binary veya sıkıştırılmış formatlar desteklenmiyor.", resultTr.Diagnostics[0].Message);

            // Act EN
            locService.SetCulture("en-US");
            var resultEn = await loader.LoadAsync(request);

            // Assert EN
            Assert.Equal(AssetPreviewStatus.Unsupported, resultEn.Status);
            Assert.Single(resultEn.Diagnostics);
            Assert.Equal(O3dDiagnosticCode.UnsupportedFormat, resultEn.Diagnostics[0].Code);
            Assert.Contains("DirectX .x mesh format is recognized but binary or compressed formats are not supported.", resultEn.Diagnostics[0].Message);
        }
        finally
        {
            if (File.Exists("model.x")) File.Delete("model.x");
        }
    }

    [Fact]
    public void Localization_Keys_ForDirectXMesh_AreDefined()
    {
        // Arrange
        var locService = new LocalizationService();

        // Act & Assert tr-TR
        locService.SetCulture("tr-TR");
        Assert.Contains(".o3d/.x", locService["MeshesSection"]);
        Assert.Contains("DirectX .x mesh formatı tanındı", locService["DirectXMeshParserPending"]);

        // Act & Assert en-US
        locService.SetCulture("en-US");
        Assert.Contains(".o3d/.x", locService["MeshesSection"]);
        Assert.Contains("DirectX .x mesh format is recognized", locService["DirectXMeshParserPending"]);
    }

    [Fact]
    public async Task LoadAsync_MultiMesh_MaterialSlotSourceModelPaths_AreSetCorrectly()
    {
        // Arrange
        var mesh1 = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() },
            Triangles = new List<O3dTriangle> { new() },
            MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "Mat1" } }
        };

        var mesh2 = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() },
            Triangles = new List<O3dTriangle> { new() },
            MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "Mat2" } }
        };

        var fakeReader = new FakeO3dGeometryReader();
        fakeReader.ReadFunc = (path, ct) =>
        {
            if (path.Contains("mesh1"))
                return new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh1 };
            return new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh2 };
        };

        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());
        var request = new AssetPreviewRequest
        {
            ModelPaths = new[] { "mesh1.o3d", "mesh2.o3d" }
        };

        File.WriteAllText("mesh1.o3d", "");
        File.WriteAllText("mesh2.o3d", "");

        try
        {
            // Act
            var result = await loader.LoadAsync(request);

            // Assert
            Assert.Equal(AssetPreviewStatus.Success, result.Status);
            var combined = result.MeshData;
            Assert.NotNull(combined);
            Assert.Equal(2, combined.MaterialSlots.Count);
            Assert.Equal("mesh1.o3d", combined.MaterialSlots[0].SourceModelPath);
            Assert.Equal("mesh2.o3d", combined.MaterialSlots[1].SourceModelPath);
        }
        finally
        {
            if (File.Exists("mesh1.o3d")) File.Delete("mesh1.o3d");
            if (File.Exists("mesh2.o3d")) File.Delete("mesh2.o3d");
        }
    }

    [Fact]
    public async Task LoadAsync_MultiMesh_AppliesTransformsCorrectly()
    {
        // Arrange
        var mesh1 = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() { X = 1f, Y = 1f, Z = 1f } },
            Triangles = new List<O3dTriangle> { new() { V0 = 0, V1 = 0, V2 = 0, MaterialSlotIndex = 0 } },
            MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "Mat1" } }
        };

        var mesh2 = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() { X = 1f, Y = 1f, Z = 1f } },
            Triangles = new List<O3dTriangle> { new() { V0 = 0, V1 = 0, V2 = 0, MaterialSlotIndex = 0 } },
            MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "Mat2" } }
        };

        var fakeReader = new FakeO3dGeometryReader();
        fakeReader.ReadFunc = (path, ct) =>
        {
            if (path.Contains("mesh1"))
                return new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh1 };
            return new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh2 };
        };

        var boundsCalculator = new FakeMeshBoundsCalculator();
        var loader = new AssetPreviewLoader(fakeReader, boundsCalculator);

        var ref1 = new OmsiModelReference("mesh1.o3d", "mesh1.o3d", true, OmsiModelReferenceResolutionStatus.Resolved)
        {
            Transform = new OmsiMeshTransform
            {
                PosX = 10, PosY = 20, PosZ = 30,
                ScaleX = 2, ScaleY = 2, ScaleZ = 2
            }
        };

        var ref2 = new OmsiModelReference("mesh2.o3d", "mesh2.o3d", true, OmsiModelReferenceResolutionStatus.Resolved)
        {
            Transform = new OmsiMeshTransform
            {
                PosX = 5, PosY = 5, PosZ = 5,
                RotZ = 90
            }
        };

        var request = new AssetPreviewRequest
        {
            ModelReferences = new[] { ref1, ref2 }
        };

        File.WriteAllText("mesh1.o3d", "");
        File.WriteAllText("mesh2.o3d", "");

        try
        {
            // Act
            var result = await loader.LoadAsync(request);

            // Assert
            Assert.Equal(AssetPreviewStatus.Success, result.Status);
            var combined = result.MeshData;
            Assert.NotNull(combined);
            Assert.Equal(2, combined.Vertices.Count);

            // Mesh 1 vertex: (1,1,1) * 2 = (2,2,2) + (10,20,30) = (12,22,32)
            Assert.Equal(12f, combined.Vertices[0].X, 1);
            Assert.Equal(22f, combined.Vertices[0].Y, 1);
            Assert.Equal(32f, combined.Vertices[0].Z, 1);

            // Mesh 2 vertex: (1,1,1) rotated Z by 90 deg = (-1,1,1) + (5,5,5) = (4,6,6)
            Assert.Equal(4f, combined.Vertices[1].X, 1);
            Assert.Equal(6f, combined.Vertices[1].Y, 1);
            Assert.Equal(6f, combined.Vertices[1].Z, 1);

            // Triangles V index offset check
            Assert.Equal(0, combined.Triangles[0].V0);
            Assert.Equal(1, combined.Triangles[1].V0);

            // Material indices offset check
            Assert.Equal(0, combined.Triangles[0].MaterialSlotIndex);
            Assert.Equal(1, combined.Triangles[1].MaterialSlotIndex);

            // Verify bounds calculated based on transformed vertices
            Assert.NotNull(result.Bounds);
        }
        finally
        {
            if (File.Exists("mesh1.o3d")) File.Delete("mesh1.o3d");
            if (File.Exists("mesh2.o3d")) File.Delete("mesh2.o3d");
        }
    }

    [Fact]
    public async Task LoadAsync_SingleMeshWithTransforms_AndApplyModelTransformsTrue_AppliesTransform()
    {
        // Arrange
        var mesh1 = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() { X = 1f, Y = 1f, Z = 1f } },
            Triangles = new List<O3dTriangle> { new() { V0 = 0, V1 = 0, V2 = 0, MaterialSlotIndex = 0 } },
            MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "Mat1" } }
        };

        var fakeReader = new FakeO3dGeometryReader();
        fakeReader.ReadFunc = (path, ct) => new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh1 };

        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());

        var ref1 = new OmsiModelReference("mesh1.o3d", "mesh1.o3d", true, OmsiModelReferenceResolutionStatus.Resolved)
        {
            Transform = new OmsiMeshTransform
            {
                PosX = 10, PosY = 20, PosZ = 30,
                ScaleX = 2, ScaleY = 2, ScaleZ = 2
            }
        };

        var request = new AssetPreviewRequest
        {
            ModelReferences = new[] { ref1 },
            ApplyModelTransforms = true
        };

        File.WriteAllText("mesh1.o3d", "");

        try
        {
            // Act
            var result = await loader.LoadAsync(request);

            // Assert
            Assert.Equal(AssetPreviewStatus.Success, result.Status);
            var combined = result.MeshData;
            Assert.NotNull(combined);
            Assert.Single(combined.Vertices);
            Assert.Equal(12f, combined.Vertices[0].X, 1);
            Assert.Equal(22f, combined.Vertices[0].Y, 1);
            Assert.Equal(32f, combined.Vertices[0].Z, 1);
        }
        finally
        {
            if (File.Exists("mesh1.o3d")) File.Delete("mesh1.o3d");
        }
    }

    [Fact]
    public async Task LoadAsync_SingleMeshWithTransforms_AndApplyModelTransformsFalse_DoesNotApplyTransform()
    {
        // Arrange
        var mesh1 = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() { X = 1f, Y = 1f, Z = 1f } },
            Triangles = new List<O3dTriangle> { new() { V0 = 0, V1 = 0, V2 = 0, MaterialSlotIndex = 0 } },
            MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "Mat1" } }
        };

        var fakeReader = new FakeO3dGeometryReader();
        fakeReader.ReadFunc = (path, ct) => new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh1 };

        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());

        var ref1 = new OmsiModelReference("mesh1.o3d", "mesh1.o3d", true, OmsiModelReferenceResolutionStatus.Resolved)
        {
            Transform = new OmsiMeshTransform
            {
                PosX = 10, PosY = 20, PosZ = 30,
                ScaleX = 2, ScaleY = 2, ScaleZ = 2
            }
        };

        var request = new AssetPreviewRequest
        {
            ModelReferences = new[] { ref1 },
            ApplyModelTransforms = false
        };

        File.WriteAllText("mesh1.o3d", "");

        try
        {
            // Act
            var result = await loader.LoadAsync(request);

            // Assert
            Assert.Equal(AssetPreviewStatus.Success, result.Status);
            var combined = result.MeshData;
            Assert.NotNull(combined);
            Assert.Single(combined.Vertices);
            Assert.Equal(1f, combined.Vertices[0].X, 1);
            Assert.Equal(1f, combined.Vertices[0].Y, 1);
            Assert.Equal(1f, combined.Vertices[0].Z, 1);
        }
        finally
        {
            if (File.Exists("mesh1.o3d")) File.Delete("mesh1.o3d");
        }
    }

    [Fact]
    public async Task LoadAsync_ExceedsMaxPreviewVertices_ReturnsUnsupportedWithSafetyWarning()
    {
        // Arrange
        int originalLimit = PreviewPerformancePolicy.MaxPreviewVertices;
        PreviewPerformancePolicy.MaxPreviewVertices = 5;

        try
        {
            var mesh = new O3dMeshData
            {
                Vertices = new List<O3dVertex>
                {
                    new() { X = 0, Y = 0, Z = 0 },
                    new() { X = 1, Y = 0, Z = 0 },
                    new() { X = 0, Y = 1, Z = 0 },
                    new() { X = 0, Y = 0, Z = 1 },
                    new() { X = 1, Y = 1, Z = 1 },
                    new() { X = 2, Y = 2, Z = 2 } // 6 vertices (> 5)
                },
                Triangles = new List<O3dTriangle> { new() { V0 = 0, V1 = 1, V2 = 2 } }
            };

            var fakeReader = new FakeO3dGeometryReader();
            fakeReader.ReadFunc = (path, ct) => new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh };

            var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());
            var request = new AssetPreviewRequest
            {
                ModelPath = "mesh1.o3d"
            };

            File.WriteAllText("mesh1.o3d", "");

            try
            {
                // Act
                var result = await loader.LoadAsync(request);

                // Assert
                Assert.Equal(AssetPreviewStatus.Unsupported, result.Status);
                Assert.Null(result.MeshData);
                Assert.Single(result.Diagnostics);
                Assert.Contains("Preview skipped because mesh is too large", result.Diagnostics[0].Message);
            }
            finally
            {
                if (File.Exists("mesh1.o3d")) File.Delete("mesh1.o3d");
            }
        }
        finally
        {
            PreviewPerformancePolicy.MaxPreviewVertices = originalLimit;
        }
    }

    [Fact]
    public async Task LoadAsync_MultiMeshWithTransformsAndMaterialBinding_ResolvesAndBindsSuccessfully()
    {
        // Arrange
        var mesh1 = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() { X = 1f, Y = 1f, Z = 1f } },
            Triangles = new List<O3dTriangle> { new() { V0 = 0, V1 = 0, V2 = 0, MaterialSlotIndex = 0 } },
            MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "Mat1", TextureReference = "tex1.png" } }
        };

        var mesh2 = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() { X = 2f, Y = 2f, Z = 2f } },
            Triangles = new List<O3dTriangle> { new() { V0 = 0, V1 = 0, V2 = 0, MaterialSlotIndex = 0 } },
            MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "Mat2", TextureReference = "tex2.png" } }
        };

        var fakeReader = new FakeO3dGeometryReader();
        fakeReader.ReadFunc = (path, ct) =>
        {
            if (path == "folderA/mesh1.o3d") return new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh1 };
            if (path == "folderB/mesh2.o3d") return new O3dGeometryReadResult { Status = O3dGeometryStatus.Success, MeshData = mesh2 };
            return new O3dGeometryReadResult { Status = O3dGeometryStatus.Failed };
        };

        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator());

        var ref1 = new OmsiModelReference("mesh1.o3d", "folderA/mesh1.o3d", true, OmsiModelReferenceResolutionStatus.Resolved)
        {
            Transform = new OmsiMeshTransform { PosX = 10, PosY = 20, PosZ = 30 }
        };

        var ref2 = new OmsiModelReference("mesh2.o3d", "folderB/mesh2.o3d", true, OmsiModelReferenceResolutionStatus.Resolved)
        {
            Transform = new OmsiMeshTransform { PosX = 100, PosY = 200, PosZ = 300 }
        };

        var request = new AssetPreviewRequest
        {
            ModelReferences = new[] { ref1, ref2 },
            ApplyModelTransforms = true
        };

        Directory.CreateDirectory("folderA");
        Directory.CreateDirectory("folderB");
        File.WriteAllText("folderA/mesh1.o3d", "");
        File.WriteAllText("folderB/mesh2.o3d", "");

        try
        {
            // Act
            var result = await loader.LoadAsync(request);

            // Assert Loader Part
            Assert.Equal(AssetPreviewStatus.Success, result.Status);
            var combined = result.MeshData;
            Assert.NotNull(combined);
            Assert.Equal(2, combined.Vertices.Count);
            // Verify mesh 1 transformed
            Assert.Equal(11f, combined.Vertices[0].X);
            // Verify mesh 2 transformed
            Assert.Equal(102f, combined.Vertices[1].X);
            
            // Verify SourceModelPath was preserved
            Assert.Equal(2, combined.MaterialSlots.Count);
            Assert.Equal("folderA/mesh1.o3d", combined.MaterialSlots[0].SourceModelPath);
            Assert.Equal("folderB/mesh2.o3d", combined.MaterialSlots[1].SourceModelPath);

            // Verify texture binding can bind with correct paths using local dummy classes
            var localResolver = new LocalResolver();
            var localLoader = new LocalLoader();

            var bindingService = new MaterialTextureBindingService(localResolver, localLoader);
            var bindings = await bindingService.BindAsync(combined, "fallback.o3d", "/root");

            Assert.Equal(2, bindings.Count);
            Assert.Equal(TextureBindingStatus.Bound, bindings[0].Status);
            Assert.Equal(TextureBindingStatus.Bound, bindings[1].Status);
            Assert.Equal("folderA/mesh1.o3d", localResolver.ResolvedPaths[0]);
            Assert.Equal("folderB/mesh2.o3d", localResolver.ResolvedPaths[1]);
        }
        finally
        {
            if (File.Exists("folderA/mesh1.o3d")) File.Delete("folderA/mesh1.o3d");
            if (File.Exists("folderB/mesh2.o3d")) File.Delete("folderB/mesh2.o3d");
            if (Directory.Exists("folderA")) Directory.Delete("folderA");
            if (Directory.Exists("folderB")) Directory.Delete("folderB");
        }
    }

    [Fact]
    public async Task LoadAsync_DirectXMesh_LoadsSuccessfullyViaDirectXReader()
    {
        // Arrange
        var fakeReader = new FakeO3dGeometryReader();
        var dxReader = new DirectXGeometryReader();
        var loader = new AssetPreviewLoader(fakeReader, new FakeMeshBoundsCalculator(), directXReader: dxReader);

        string content = 
@"xof 0302txt 0064
Mesh Cube {
  3;
  0.0;0.0;0.0;,
  1.0;0.0;0.0;,
  0.0;1.0;0.0;;
  1;
  3; 0, 1, 2;;
}";
        string path = "test_mesh.x";
        File.WriteAllText(path, content);

        try
        {
            var ref1 = new OmsiModelReference("test_mesh.x", path, true, OmsiModelReferenceResolutionStatus.Resolved);
            var request = new AssetPreviewRequest
            {
                ModelReferences = new[] { ref1 },
                ApplyModelTransforms = false
            };

            // Act
            var result = await loader.LoadAsync(request);

            // Assert
            Assert.Equal(AssetPreviewStatus.Success, result.Status);
            Assert.NotNull(result.MeshData);
            Assert.Equal(3, result.MeshData.Vertices.Count);
            Assert.Single(result.MeshData.Triangles);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_MixedO3dAndDirectXMesh_CombinesMeshesSuccessfully()
    {
        // Arrange
        var fakeO3dMeshData = new O3dMeshData
        {
            Vertices = new List<O3dVertex> { new() { X = 10f, Y = 10f, Z = 10f } },
            Triangles = new List<O3dTriangle> { new() { V0 = 0, V1 = 0, V2 = 0, MaterialSlotIndex = 0 } },
            MaterialSlots = new List<O3dMaterialSlot> { new() { MaterialName = "O3dMat", TextureReference = "o3d_tex.png" } }
        };

        var fakeO3dReader = new FakeO3dGeometryReader();
        fakeO3dReader.ReadFunc = (p, ct) => new O3dGeometryReadResult
        {
            Status = O3dGeometryStatus.Success,
            MeshData = fakeO3dMeshData
        };

        var dxReader = new DirectXGeometryReader();
        var loader = new AssetPreviewLoader(fakeO3dReader, new FakeMeshBoundsCalculator(), directXReader: dxReader);

        string dxContent = 
@"xof 0302txt 0064
Mesh Cube {
  3;
  0.0;0.0;0.0;,
  1.0;0.0;0.0;,
  0.0;1.0;0.0;;
  1;
  3; 0, 1, 2;;
}";
        string o3dPath = "mesh.o3d";
        string dxPath = "mesh.x";
        File.WriteAllText(o3dPath, "dummy o3d");
        File.WriteAllText(dxPath, dxContent);

        try
        {
            var ref1 = new OmsiModelReference("mesh.o3d", o3dPath, true, OmsiModelReferenceResolutionStatus.Resolved);
            var ref2 = new OmsiModelReference("mesh.x", dxPath, true, OmsiModelReferenceResolutionStatus.Resolved);
            var request = new AssetPreviewRequest
            {
                ModelReferences = new[] { ref1, ref2 },
                ApplyModelTransforms = false
            };

            // Act
            var result = await loader.LoadAsync(request);

            // Assert
            Assert.Equal(AssetPreviewStatus.Success, result.Status);
            Assert.NotNull(result.MeshData);
            // Combined vertex count: 1 from O3D + 3 from DirectX = 4 vertices
            Assert.Equal(4, result.MeshData.Vertices.Count);
            // Combined triangles count: 1 from O3D + 1 from DirectX = 2 triangles
            Assert.Equal(2, result.MeshData.Triangles.Count);
        }
        finally
        {
            if (File.Exists(o3dPath)) File.Delete(o3dPath);
            if (File.Exists(dxPath)) File.Delete(dxPath);
        }
    }

    private class LocalResolver : IOmsiTextureReferenceResolver
    {
        public List<string> ResolvedPaths { get; } = new();
        public OmsiTextureReference Resolve(string texturePath, string modelFilePath, string sceneryObjectsRoot)
        {
            ResolvedPaths.Add(modelFilePath);
            return new OmsiTextureReference { TexturePath = texturePath, ResolvedPath = "/res/" + texturePath, Exists = true, ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved };
        }
    }

    private class LocalLoader : ITextureImageLoader
    {
        public Task<TextureLoadResult> LoadAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TextureLoadResult { Status = TextureLoadStatus.Success, Image = new TextureImageData { Width = 2, Height = 2 } });
    }
}
