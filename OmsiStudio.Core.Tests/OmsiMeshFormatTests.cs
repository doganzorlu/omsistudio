using Xunit;
using OmsiStudio.Core.Assets;

namespace OmsiStudio.Core.Tests;

public class OmsiMeshFormatTests
{
    [Theory]
    [InlineData("model.o3d", OmsiMeshFormat.O3d)]
    [InlineData("model.O3D", OmsiMeshFormat.O3d)]
    [InlineData("model.x", OmsiMeshFormat.DirectX)]
    [InlineData("model.X", OmsiMeshFormat.DirectX)]
    [InlineData("model.obj", OmsiMeshFormat.Unsupported)]
    [InlineData("model.fbx", OmsiMeshFormat.Unsupported)]
    [InlineData("", OmsiMeshFormat.Unsupported)]
    [InlineData(null, OmsiMeshFormat.Unsupported)]
    public void DetectFormat_ReturnsExpectedFormat(string? path, OmsiMeshFormat expected)
    {
        // Act
        var result = OmsiMeshFormatHelper.DetectFormat(path);

        // Assert
        Assert.Equal(expected, result);
    }
}
