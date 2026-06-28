using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using Xunit;

namespace OmsiStudio.Core.Tests;

public class DirectXGeometryReaderTests : IDisposable
{
    private readonly string _tempFile;

    public DirectXGeometryReaderTests()
    {
        _tempFile = Path.GetTempFileName() + ".x";
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [Fact]
    public async Task ReadAsync_BinaryHeader_ReturnsUnsupported()
    {
        // Arrange
        byte[] binaryHeader = new byte[16];
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("xof 0303bin 0064"), binaryHeader, 16);
        File.WriteAllBytes(_tempFile, binaryHeader);

        var reader = new DirectXGeometryReader();

        // Act
        var result = await reader.ReadAsync(_tempFile);

        // Assert
        Assert.Equal(O3dGeometryStatus.Unsupported, result.Status);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.UnsupportedFormat, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task ReadAsync_MinimalTextTriangle_Succeeds()
    {
        // Arrange
        string content = 
@"xof 0302txt 0064
Mesh Cube {
  3;
  0.0; 0.0; 0.0;,
  1.0; 0.0; 0.0;,
  0.0; 1.0; 0.0;;
  1;
  3; 0, 1, 2;;
}";
        File.WriteAllText(_tempFile, content);
        var reader = new DirectXGeometryReader();

        // Act
        var result = await reader.ReadAsync(_tempFile);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        var mesh = result.MeshData;
        Assert.NotNull(mesh);
        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Single(mesh.Triangles);
        Assert.Equal(0, mesh.Triangles[0].V0);
        Assert.Equal(1, mesh.Triangles[0].V1);
        Assert.Equal(2, mesh.Triangles[0].V2);
    }

    [Fact]
    public async Task ReadAsync_QuadFace_FanTriangulatesIntoTwoTriangles()
    {
        // Arrange
        string content = 
@"xof 0302txt 0064
Mesh Quad {
  4;
  0.0;0.0;0.0;,
  1.0;0.0;0.0;,
  1.0;1.0;0.0;,
  0.0;1.0;0.0;;
  1;
  4; 0, 1, 2, 3;;
}";
        File.WriteAllText(_tempFile, content);
        var reader = new DirectXGeometryReader();

        // Act
        var result = await reader.ReadAsync(_tempFile);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        var mesh = result.MeshData;
        Assert.NotNull(mesh);
        Assert.Equal(2, mesh.Triangles.Count);
        // Triangle 1: (0, 1, 2)
        Assert.Equal(0, mesh.Triangles[0].V0);
        Assert.Equal(1, mesh.Triangles[0].V1);
        Assert.Equal(2, mesh.Triangles[0].V2);
        // Triangle 2: (0, 2, 3)
        Assert.Equal(0, mesh.Triangles[1].V0);
        Assert.Equal(2, mesh.Triangles[1].V1);
        Assert.Equal(3, mesh.Triangles[1].V2);
    }

    [Fact]
    public async Task ReadAsync_UVCoordsAndNormals_ParsesCorrectlyAndIgnoresNormals()
    {
        // Arrange
        string content = 
@"xof 0302txt 0064
Mesh Cube {
  3;
  0.0;0.0;0.0;,
  1.0;0.0;0.0;,
  0.0;1.0;0.0;;
  1;
  3; 0, 1, 2;;
  
  MeshNormals {
    3;
    0.0;0.0;1.0;,
    0.0;0.0;1.0;,
    0.0;0.0;1.0;;
    1;
    3; 0,1,2;;
  }

  MeshTextureCoords {
    3;
    0.1; 0.2;,
    0.3; 0.4;,
    0.5; 0.6;;
  }
}";
        File.WriteAllText(_tempFile, content);
        var reader = new DirectXGeometryReader();

        // Act
        var result = await reader.ReadAsync(_tempFile);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        var mesh = result.MeshData;
        Assert.NotNull(mesh);
        Assert.Equal(0.1f, mesh.Vertices[0].Uv.U);
        Assert.Equal(0.2f, mesh.Vertices[0].Uv.V);
        Assert.Equal(0.3f, mesh.Vertices[1].Uv.U);
        Assert.Equal(0.4f, mesh.Vertices[1].Uv.V);
    }

    [Fact]
    public async Task ReadAsync_MaterialAndTexture_MapsIntoMaterialSlot()
    {
        // Arrange
        string content = 
@"xof 0302txt 0064
Mesh Cube {
  3;
  0.0;0.0;0.0;,
  1.0;0.0;0.0;,
  0.0;1.0;0.0;;
  1;
  3; 0, 1, 2;;

  MeshMaterialList {
    1;
    1;
    0;;
    Material Material0 {
      1.0;1.0;1.0;1.0;;
      3.14;
      1.0;1.0;1.0;;
      1.0;1.0;1.0;;
      TextureFilename {
        ""brick.png"";
      }
    }
  }
}";
        File.WriteAllText(_tempFile, content);
        var reader = new DirectXGeometryReader();

        // Act
        var result = await reader.ReadAsync(_tempFile);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        var mesh = result.MeshData;
        Assert.NotNull(mesh);
        Assert.Single(mesh.MaterialSlots);
        Assert.Equal("Material0", mesh.MaterialSlots[0].MaterialName);
        Assert.Equal("brick.png", mesh.MaterialSlots[0].TextureReference);
    }

    [Fact]
    public async Task ReadAsync_InvalidVertexIndex_ReturnsInvalidIndexDiagnostic()
    {
        // Arrange
        string content = 
@"xof 0302txt 0064
Mesh Cube {
  3;
  0.0;0.0;0.0;,
  1.0;0.0;0.0;,
  0.0;1.0;0.0;;
  1;
  3; 0, 1, 99;; // Vertex 99 is invalid
}";
        File.WriteAllText(_tempFile, content);
        var reader = new DirectXGeometryReader();

        // Act
        var result = await reader.ReadAsync(_tempFile);

        // Assert
        Assert.Equal(O3dGeometryStatus.Invalid, result.Status);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.InvalidIndex, result.Diagnostics[0].Code);
    }

    [Fact]
    public async Task ReadAsync_ExceedsMaxVertices_ReturnsSafetyDiagnostic()
    {
        // Arrange
        int originalLimit = PreviewPerformancePolicy.MaxPreviewVertices;
        PreviewPerformancePolicy.MaxPreviewVertices = 2;

        try
        {
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
            File.WriteAllText(_tempFile, content);
            var reader = new DirectXGeometryReader();

            // Act
            var result = await reader.ReadAsync(_tempFile);

            // Assert
            Assert.Equal(O3dGeometryStatus.Unsupported, result.Status);
            Assert.Single(result.Diagnostics);
            Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, result.Diagnostics[0].Code);
        }
        finally
        {
            PreviewPerformancePolicy.MaxPreviewVertices = originalLimit;
        }
    }

    [Fact]
    public async Task ReadAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var reader = new DirectXGeometryReader();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(_tempFile, cts.Token));
    }

    [Fact]
    public async Task ReadAsync_ParsingLoopCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
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
        File.WriteAllText(_tempFile, content);
        var reader = new DirectXGeometryReader();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(_tempFile, cts.Token));
    }

    [Fact]
    public async Task ReadAsync_PolygonFanTriangulationExceedsLimit_ReturnsSafetyLimitExceeded()
    {
        // Arrange
        int originalLimit = PreviewPerformancePolicy.MaxPreviewTriangles;
        PreviewPerformancePolicy.MaxPreviewTriangles = 1;

        try
        {
            string content = 
@"xof 0302txt 0064
Mesh Quad {
  4;
  0.0;0.0;0.0;,
  1.0;0.0;0.0;,
  1.0;1.0;0.0;,
  0.0;1.0;0.0;;
  1;
  4; 0, 1, 2, 3;;
}";
            File.WriteAllText(_tempFile, content);
            var reader = new DirectXGeometryReader();

            // Act
            var result = await reader.ReadAsync(_tempFile);

            // Assert
            Assert.Equal(O3dGeometryStatus.Unsupported, result.Status);
            Assert.Single(result.Diagnostics);
            Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, result.Diagnostics[0].Code);
            Assert.Contains("Triangles count exceeds MaxPreviewTriangles", result.Diagnostics[0].Message);
        }
        finally
        {
            PreviewPerformancePolicy.MaxPreviewTriangles = originalLimit;
        }
    }

    [Fact]
    public async Task ReadAsync_InvalidMaterialIndex_ReturnsInvalidIndex()
    {
        // Arrange
        string content = 
@"xof 0302txt 0064
Mesh Cube {
  3;
  0.0;0.0;0.0;,
  1.0;0.0;0.0;,
  0.0;1.0;0.0;;
  1;
  3; 0, 1, 2;;

  MeshMaterialList {
    1;
    1;
    99;;
    Material Material0 {
      1.0;1.0;1.0;1.0;;
      3.14;
      1.0;1.0;1.0;;
      1.0;1.0;1.0;;
      TextureFilename {
        ""brick.png"";
      }
    }
  }
}";
        File.WriteAllText(_tempFile, content);
        var reader = new DirectXGeometryReader();

        // Act
        var result = await reader.ReadAsync(_tempFile);

        // Assert
        Assert.Equal(O3dGeometryStatus.Invalid, result.Status);
        Assert.Single(result.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.InvalidIndex, result.Diagnostics[0].Code);
        Assert.Contains("material index 99 is out of bounds", result.Diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_QuotedTextureFilenameWithCommentsSymbols_ParsesIntact()
    {
        // Arrange
        string content = 
@"xof 0302txt 0064
Mesh Cube {
  3;
  0.0;0.0;0.0;,
  1.0;0.0;0.0;,
  0.0;1.0;0.0;;
  1;
  3; 0, 1, 2;;

  MeshMaterialList {
    1;
    1;
    0;;
    Material Material0 {
      1.0;1.0;1.0;1.0;;
      3.14;
      1.0;1.0;1.0;;
      1.0;1.0;1.0;;
      TextureFilename {
        ""tex#dir//file.png"";
      }
    }
  }
}";
        File.WriteAllText(_tempFile, content);
        var reader = new DirectXGeometryReader();

        // Act
        var result = await reader.ReadAsync(_tempFile);

        // Assert
        Assert.Equal(O3dGeometryStatus.Success, result.Status);
        var mesh = result.MeshData;
        Assert.NotNull(mesh);
        Assert.Single(mesh.MaterialSlots);
        Assert.Equal("tex#dir//file.png", mesh.MaterialSlots[0].TextureReference);
    }

    [Fact]
    public async Task ReadAsync_NegativeCountsAndMalformedFace_ReturnsInvalidCountDiagnostic()
    {
        // Arrange
        string content1 = 
@"xof 0302txt 0064
Mesh Cube {
  -3;
}";
        File.WriteAllText(_tempFile, content1);
        var reader = new DirectXGeometryReader();
        var result1 = await reader.ReadAsync(_tempFile);
        Assert.Equal(O3dGeometryStatus.Invalid, result1.Status);
        Assert.Equal(O3dDiagnosticCode.InvalidCount, result1.Diagnostics[0].Code);

        string content2 = 
@"xof 0302txt 0064
Mesh Cube {
  3;
  0.0;0.0;0.0;,
  1.0;0.0;0.0;,
  0.0;1.0;0.0;;
  -1;
}";
        File.WriteAllText(_tempFile, content2);
        var result2 = await reader.ReadAsync(_tempFile);
        Assert.Equal(O3dGeometryStatus.Invalid, result2.Status);
        Assert.Equal(O3dDiagnosticCode.InvalidCount, result2.Diagnostics[0].Code);

        string content3 = 
@"xof 0302txt 0064
Mesh Cube {
  3;
  0.0;0.0;0.0;,
  1.0;0.0;0.0;,
  0.0;1.0;0.0;;
  1;
  2; 0, 1;;
}";
        File.WriteAllText(_tempFile, content3);
        var result3 = await reader.ReadAsync(_tempFile);
        Assert.Equal(O3dGeometryStatus.Invalid, result3.Status);
        Assert.Equal(O3dDiagnosticCode.InvalidCount, result3.Diagnostics[0].Code);
    }
}
