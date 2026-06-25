using Xunit;
using OmsiStudio.Core.Assets;
using OmsiStudio.OmsiFormat.Parser;

namespace OmsiStudio.OmsiFormat.Tests;

public class O3dGeometrySafetyPolicyTests
{
    [Fact]
    public void ValidateCounts_WithValidCounts_ReturnsTrue()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateCounts(1, 100, 50, 2, 0, out var diagnostic);

        // Assert
        Assert.True(result);
        Assert.Null(diagnostic);
    }

    [Theory]
    [InlineData(-1, 100, 50, 2)]
    [InlineData(1, -100, 50, 2)]
    [InlineData(1, 100, -50, 2)]
    [InlineData(1, 100, 50, -2)]
    public void ValidateCounts_WithNegativeCounts_ReturnsFalseAndInvalidCount(long mesh, long vertex, long triangle, long material)
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateCounts(mesh, vertex, triangle, material, 12, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.InvalidCount, diagnostic.Code);
        Assert.Equal(O3dDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(12, diagnostic.ByteOffset);
    }

    [Fact]
    public void ValidateCounts_WithMeshCountExceedingLimit_ReturnsFalseAndSafetyLimitExceeded()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateCounts(100_001, 100, 50, 2, 0, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, diagnostic.Code);
    }

    [Fact]
    public void ValidateCounts_WithVertexCountExceedingLimit_ReturnsFalseAndSafetyLimitExceeded()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateCounts(1, 1_000_001, 50, 2, 0, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, diagnostic.Code);
    }

    [Fact]
    public void ValidateCounts_WithTriangleCountExceedingLimit_ReturnsFalseAndSafetyLimitExceeded()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateCounts(1, 100, 1_000_001, 2, 0, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, diagnostic.Code);
    }

    [Fact]
    public void ValidateCounts_WithMaterialCountExceedingLimit_ReturnsFalseAndSafetyLimitExceeded()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateCounts(1, 100, 50, 100_001, 0, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, diagnostic.Code);
    }

    [Fact]
    public void ValidateVertexBlock_WithValidSize_ReturnsTrue()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateVertexBlock(10, 320, 0, out var diagnostic);

        // Assert
        Assert.True(result);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void ValidateVertexBlock_WithNegativeCount_ReturnsFalseAndInvalidCount()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateVertexBlock(-10, 320, 15, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.InvalidCount, diagnostic.Code);
        Assert.Equal(15, diagnostic.ByteOffset);
    }

    [Fact]
    public void ValidateVertexBlock_WithArithmeticOverflow_ReturnsFalseAndSafetyLimitExceeded()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateVertexBlock(long.MaxValue / 2, 1000, 0, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, diagnostic.Code);
    }

    [Fact]
    public void ValidateVertexBlock_WithInsufficientStreamBytes_ReturnsFalseAndTruncatedStream()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateVertexBlock(10, 319, 20, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.TruncatedStream, diagnostic.Code);
        Assert.Equal(20, diagnostic.ByteOffset);
    }

    [Theory]
    [InlineData(false, 80)]
    [InlineData(true, 140)]
    public void ValidateFaceBlock_WithValidSize_ReturnsTrue(bool useLong, long remaining)
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateFaceBlock(10, useLong, remaining, 0, out var diagnostic);

        // Assert
        Assert.True(result);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void ValidateFaceBlock_WithNegativeCount_ReturnsFalseAndInvalidCount()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateFaceBlock(-10, false, 80, 25, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.InvalidCount, diagnostic.Code);
        Assert.Equal(25, diagnostic.ByteOffset);
    }

    [Fact]
    public void ValidateFaceBlock_WithArithmeticOverflow_ReturnsFalseAndSafetyLimitExceeded()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateFaceBlock(long.MaxValue / 2, false, 1000, 0, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, diagnostic.Code);
    }

    [Theory]
    [InlineData(false, 79)]
    [InlineData(true, 139)]
    public void ValidateFaceBlock_WithInsufficientStreamBytes_ReturnsFalseAndTruncatedStream(bool useLong, long remaining)
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateFaceBlock(10, useLong, remaining, 30, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.TruncatedStream, diagnostic.Code);
        Assert.Equal(30, diagnostic.ByteOffset);
    }

    [Fact]
    public void ValidateStringLength_WithValidString_ReturnsTrue()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateStringLength(50, 100, 0, out var diagnostic);

        // Assert
        Assert.True(result);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void ValidateStringLength_WithNegativeLength_ReturnsFalseAndInvalidCount()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateStringLength(-5, 100, 40, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.InvalidCount, diagnostic.Code);
        Assert.Equal(40, diagnostic.ByteOffset);
    }

    [Fact]
    public void ValidateStringLength_WithExceededLimit_ReturnsFalseAndStringLengthExceeded()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateStringLength(1025, 2000, 0, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.StringLengthExceeded, diagnostic.Code);
    }

    [Fact]
    public void ValidateStringLength_WithInsufficientStreamBytes_ReturnsFalseAndInvalidStringBounds()
    {
        // Act
        var result = O3dGeometrySafetyPolicy.ValidateStringLength(50, 49, 50, out var diagnostic);

        // Assert
        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(O3dDiagnosticCode.InvalidStringBounds, diagnostic.Code);
        Assert.Equal(50, diagnostic.ByteOffset);
    }
}
