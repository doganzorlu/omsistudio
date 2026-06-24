using System;
using System.IO;
using Xunit;

namespace OmsiStudio.OmsiFormat.Tests;

public class O3dFixtureTests
{
    private static readonly string FixtureDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "O3d");

    [Theory]
    [InlineData("valid_v3_header.o3d", 35)]
    [InlineData("truncated_header.o3d", 6)]
    [InlineData("dos_excessive_count.o3d", 20)]
    [InlineData("invalid_string_bounds.o3d", 29)]
    [InlineData("encrypted_marker.o3d", 8)]
    public void O3dFixtures_ExistAndHaveExpectedSizes(string filename, int expectedSize)
    {
        // Arrange
        var filePath = Path.Combine(FixtureDirectory, filename);

        // Act & Assert
        Assert.True(File.Exists(filePath), $"Fixture file {filename} does not exist at path: {filePath}");
        
        var fileInfo = new FileInfo(filePath);
        Assert.Equal(expectedSize, fileInfo.Length);
        Assert.True(fileInfo.Length > 0, $"Fixture file {filename} should be non-empty.");
    }
}
