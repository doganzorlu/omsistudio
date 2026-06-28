using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.App.Services;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;
using Xunit;

namespace OmsiStudio.App.Tests;

public class MaterialTextureBindingServiceTests
{
    private class FakeTextureResolver : IOmsiTextureReferenceResolver
    {
        public Func<string, string, string, OmsiTextureReference>? OnResolve { get; set; }

        public OmsiTextureReference Resolve(string texturePath, string modelFilePath, string sceneryObjectsRoot)
        {
            return OnResolve?.Invoke(texturePath, modelFilePath, sceneryObjectsRoot)
                ?? new OmsiTextureReference
                {
                    TexturePath = texturePath,
                    ResolutionStatus = OmsiTextureReferenceResolutionStatus.Missing
                };
        }
    }

    private class FakeTextureImageLoader : ITextureImageLoader
    {
        public Func<string, CancellationToken, Task<TextureLoadResult>>? OnLoadAsync { get; set; }

        public Task<TextureLoadResult> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return OnLoadAsync?.Invoke(filePath, cancellationToken)
                ?? Task.FromResult(new TextureLoadResult
                {
                    Status = TextureLoadStatus.Failed
                });
        }
    }

    [Fact]
    public async Task BindAsync_SuccessfulBind_ReturnsBoundStatusWithImage()
    {
        // Arrange
        var fakeResolver = new FakeTextureResolver
        {
            OnResolve = (path, model, root) => new OmsiTextureReference
            {
                TexturePath = path,
                ResolvedPath = "/resolved/brick.png",
                Exists = true,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
            }
        };

        var fakeLoader = new FakeTextureImageLoader
        {
            OnLoadAsync = (path, token) => Task.FromResult(new TextureLoadResult
            {
                Status = TextureLoadStatus.Success,
                Image = new TextureImageData
                {
                    Width = 128,
                    Height = 128,
                    Format = TextureImageFormat.Png,
                    PixelsRgba32 = new byte[] { 255, 0, 0, 255 }
                }
            })
        };

        var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

        var meshData = new O3dMeshData
        {
            MaterialSlots = [new O3dMaterialSlot { MaterialName = "BrickMat", TextureReference = "brick.png" }]
        };

        // Act
        var result = await service.BindAsync(meshData, "model.o3d", "/root");

        // Assert
        Assert.Single(result);
        var binding = result[0];
        Assert.Equal(0, binding.MaterialIndex);
        Assert.Equal("BrickMat", binding.MaterialName);
        Assert.Equal("brick.png", binding.TextureReference);
        Assert.Equal(TextureBindingStatus.Bound, binding.Status);
        Assert.NotNull(binding.Image);
        Assert.Equal(128, binding.Image.Width);
    }

    [Fact]
    public async Task BindAsync_MissingTextureReference_ReturnsMissingStatusWithoutCallingResolver()
    {
        // Arrange
        bool resolverCalled = false;
        var fakeResolver = new FakeTextureResolver
        {
            OnResolve = (path, model, root) =>
            {
                resolverCalled = true;
                return new OmsiTextureReference { TexturePath = path, ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved };
            }
        };

        var fakeLoader = new FakeTextureImageLoader();
        var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

        var meshData = new O3dMeshData
        {
            MaterialSlots = [new O3dMaterialSlot { MaterialName = "NoTexMat", TextureReference = null }]
        };

        // Act
        var result = await service.BindAsync(meshData, "model.o3d", "/root");

        // Assert
        Assert.Single(result);
        var binding = result[0];
        Assert.Equal(TextureBindingStatus.Missing, binding.Status);
        Assert.False(resolverCalled);
        Assert.Single(binding.Diagnostics);
        Assert.Contains("has no texture reference", binding.Diagnostics[0].Message);
    }

    [Fact]
    public async Task BindAsync_ResolverMissing_ReturnsMissingStatus()
    {
        // Arrange
        var fakeResolver = new FakeTextureResolver
        {
            OnResolve = (path, model, root) => new OmsiTextureReference
            {
                TexturePath = path,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.Missing
            }
        };

        var fakeLoader = new FakeTextureImageLoader();
        var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

        var meshData = new O3dMeshData
        {
            MaterialSlots = [new O3dMaterialSlot { MaterialName = "MissingMat", TextureReference = "missing.png" }]
        };

        // Act
        var result = await service.BindAsync(meshData, "model.o3d", "/root");

        // Assert
        Assert.Single(result);
        var binding = result[0];
        Assert.Equal(TextureBindingStatus.Missing, binding.Status);
        Assert.Single(binding.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.FileNotFound, binding.Diagnostics[0].Code);
        Assert.Contains("is missing", binding.Diagnostics[0].Message);
    }

    [Fact]
    public async Task BindAsync_ResolverInvalidPath_ReturnsInvalidStatus()
    {
        // Arrange
        var fakeResolver = new FakeTextureResolver
        {
            OnResolve = (path, model, root) => new OmsiTextureReference
            {
                TexturePath = path,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.InvalidPath
            }
        };

        var fakeLoader = new FakeTextureImageLoader();
        var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

        var meshData = new O3dMeshData
        {
            MaterialSlots = [new O3dMaterialSlot { MaterialName = "BadMat", TextureReference = "../bad.png" }]
        };

        // Act
        var result = await service.BindAsync(meshData, "model.o3d", "/root");

        // Assert
        Assert.Single(result);
        var binding = result[0];
        Assert.Equal(TextureBindingStatus.Invalid, binding.Status);
        Assert.Single(binding.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.InvalidPath, binding.Diagnostics[0].Code);
        Assert.Contains("violates path traversal", binding.Diagnostics[0].Message);
    }

    [Fact]
    public async Task BindAsync_LoaderUnsupported_ReturnsUnsupportedStatus()
    {
        // Arrange
        var fakeResolver = new FakeTextureResolver
        {
            OnResolve = (path, model, root) => new OmsiTextureReference
            {
                TexturePath = path,
                ResolvedPath = "/resolved/brick.dds",
                Exists = true,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
            }
        };

        var fakeLoader = new FakeTextureImageLoader
        {
            OnLoadAsync = (path, token) => Task.FromResult(new TextureLoadResult
            {
                Status = TextureLoadStatus.UnsupportedFormat,
                Diagnostics = [new O3dDiagnostic
                {
                    Severity = O3dDiagnosticSeverity.Warning,
                    Code = O3dDiagnosticCode.UnsupportedFormat,
                    ByteOffset = 256,
                    Context = "DDS_Format_Check",
                    Message = "DDS unsupported"
                }]
            })
        };

        var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

        var meshData = new O3dMeshData
        {
            MaterialSlots = [new O3dMaterialSlot { MaterialName = "DdsMat", TextureReference = "brick.dds" }]
        };

        // Act
        var result = await service.BindAsync(meshData, "model.o3d", "/root");

        // Assert
        Assert.Single(result);
        var binding = result[0];
        Assert.Equal(TextureBindingStatus.Unsupported, binding.Status);
        Assert.Single(binding.Diagnostics);
        Assert.Equal(O3dDiagnosticCode.UnsupportedFormat, binding.Diagnostics[0].Code);
        Assert.Equal(256, binding.Diagnostics[0].ByteOffset);
        Assert.Equal("DDS_Format_Check", binding.Diagnostics[0].Context);
        Assert.Contains("DDS unsupported", binding.Diagnostics[0].Message);
        Assert.StartsWith("[Material 0 - DdsMat]", binding.Diagnostics[0].Message);
    }

    [Fact]
    public async Task BindAsync_LoaderInvalid_ReturnsInvalidStatus()
    {
        // Arrange
        var fakeResolver = new FakeTextureResolver
        {
            OnResolve = (path, model, root) => new OmsiTextureReference
            {
                TexturePath = path,
                ResolvedPath = "/resolved/corrupted.png",
                Exists = true,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
            }
        };

        var fakeLoader = new FakeTextureImageLoader
        {
            OnLoadAsync = (path, token) => Task.FromResult(new TextureLoadResult
            {
                Status = TextureLoadStatus.Invalid,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Message = "Bad magic header" }]
            })
        };

        var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

        var meshData = new O3dMeshData
        {
            MaterialSlots = [new O3dMaterialSlot { MaterialName = "CorruptMat", TextureReference = "corrupted.png" }]
        };

        // Act
        var result = await service.BindAsync(meshData, "model.o3d", "/root");

        // Assert
        Assert.Single(result);
        var binding = result[0];
        Assert.Equal(TextureBindingStatus.Invalid, binding.Status);
        Assert.Single(binding.Diagnostics);
        Assert.Contains("Bad magic header", binding.Diagnostics[0].Message);
    }

    [Fact]
    public async Task BindAsync_LoaderTooLarge_ReturnsTooLargeStatus()
    {
        // Arrange
        var fakeResolver = new FakeTextureResolver
        {
            OnResolve = (path, model, root) => new OmsiTextureReference
            {
                TexturePath = path,
                ResolvedPath = "/resolved/huge.png",
                Exists = true,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
            }
        };

        var fakeLoader = new FakeTextureImageLoader
        {
            OnLoadAsync = (path, token) => Task.FromResult(new TextureLoadResult
            {
                Status = TextureLoadStatus.TooLarge,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Warning, Message = "Dimensions exceed policy" }]
            })
        };

        var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

        var meshData = new O3dMeshData
        {
            MaterialSlots = [new O3dMaterialSlot { MaterialName = "HugeMat", TextureReference = "huge.png" }]
        };

        // Act
        var result = await service.BindAsync(meshData, "model.o3d", "/root");

        // Assert
        Assert.Single(result);
        var binding = result[0];
        Assert.Equal(TextureBindingStatus.TooLarge, binding.Status);
        Assert.Single(binding.Diagnostics);
        Assert.Contains("exceed policy", binding.Diagnostics[0].Message);
    }

    [Fact]
    public async Task BindAsync_LoaderFailed_ReturnsFailedStatus()
    {
        // Arrange
        var fakeResolver = new FakeTextureResolver
        {
            OnResolve = (path, model, root) => new OmsiTextureReference
            {
                TexturePath = path,
                ResolvedPath = "/resolved/failed.png",
                Exists = true,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
            }
        };

        var fakeLoader = new FakeTextureImageLoader
        {
            OnLoadAsync = (path, token) => Task.FromResult(new TextureLoadResult
            {
                Status = TextureLoadStatus.Failed,
                Diagnostics = [new O3dDiagnostic { Severity = O3dDiagnosticSeverity.Error, Message = "IO Exception reading file" }]
            })
        };

        var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

        var meshData = new O3dMeshData
        {
            MaterialSlots = [new O3dMaterialSlot { MaterialName = "FailedMat", TextureReference = "failed.png" }]
        };

        // Act
        var result = await service.BindAsync(meshData, "model.o3d", "/root");

        // Assert
        Assert.Single(result);
        var binding = result[0];
        Assert.Equal(TextureBindingStatus.Failed, binding.Status);
        Assert.Single(binding.Diagnostics);
        Assert.Contains("IO Exception reading file", binding.Diagnostics[0].Message);
    }

    [Fact]
    public async Task BindAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var fakeResolver = new FakeTextureResolver();
        var fakeLoader = new FakeTextureImageLoader();
        var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

        var meshData = new O3dMeshData
        {
            MaterialSlots = [new O3dMaterialSlot { MaterialName = "BrickMat", TextureReference = "brick.png" }]
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.BindAsync(meshData, "model.o3d", "/root", cts.Token));
    }

    [Fact]
    public async Task BindAsync_MultipleSlots_PreservesOrderAndIndex()
    {
        // Arrange
        var fakeResolver = new FakeTextureResolver
        {
            OnResolve = (path, model, root) => new OmsiTextureReference
            {
                TexturePath = path,
                ResolvedPath = $"/resolved/{path}",
                Exists = true,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
            }
        };

        var fakeLoader = new FakeTextureImageLoader
        {
            OnLoadAsync = (path, token) => Task.FromResult(new TextureLoadResult
            {
                Status = TextureLoadStatus.Success,
                Image = new TextureImageData { Width = 64, Height = 64 }
            })
        };

        var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

        var meshData = new O3dMeshData
        {
            MaterialSlots = [
                new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "tex0.png" },
                new O3dMaterialSlot { MaterialName = "Mat1", TextureReference = "tex1.png" },
                new O3dMaterialSlot { MaterialName = "Mat2", TextureReference = "tex2.png" }
            ]
        };

        // Act
        var result = await service.BindAsync(meshData, "model.o3d", "/root");

        // Assert
        Assert.Equal(3, result.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(i, result[i].MaterialIndex);
            Assert.Equal($"Mat{i}", result[i].MaterialName);
            Assert.Equal($"tex{i}.png", result[i].TextureReference);
            Assert.Equal(TextureBindingStatus.Bound, result[i].Status);
        }
    }
}
