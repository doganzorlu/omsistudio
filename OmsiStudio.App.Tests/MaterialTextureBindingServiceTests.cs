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

    [Fact]
    public async Task BindAsync_WithMultipleSourceModelPaths_ResolvesEachTextureAgainstItsOwnFolder()
    {
        // Arrange
        var resolvedPaths = new List<string>();
        var fakeResolver = new FakeTextureResolver
        {
            OnResolve = (path, model, root) =>
            {
                resolvedPaths.Add(model);
                return new OmsiTextureReference
                {
                    TexturePath = "resolved_" + path,
                    ResolvedPath = "/resolved/" + path,
                    ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
                };
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
                new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "tex0.png", SourceModelPath = "/folderA/modelA.o3d" },
                new O3dMaterialSlot { MaterialName = "Mat1", TextureReference = "tex1.png", SourceModelPath = "/folderB/modelB.o3d" }
            ]
        };

        // Act
        var result = await service.BindAsync(meshData, "/fallback/fallback.o3d", "/root");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("/folderA/modelA.o3d", resolvedPaths[0]);
        Assert.Equal("/folderB/modelB.o3d", resolvedPaths[1]);
    }

    [Fact]
    public async Task BindAsync_SingleMesh_FallbackToModelFilePath()
    {
        // Arrange
        var resolvedPaths = new List<string>();
        var fakeResolver = new FakeTextureResolver
        {
            OnResolve = (path, model, root) =>
            {
                resolvedPaths.Add(model);
                return new OmsiTextureReference
                {
                    TexturePath = "resolved_" + path,
                    ResolvedPath = "/resolved/" + path,
                    ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
                };
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
                new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "tex0.png" }
            ]
        };

        // Act
        var result = await service.BindAsync(meshData, "/fallback/fallback.o3d", "/root");

        // Assert
        Assert.Single(result);
        Assert.Equal("/fallback/fallback.o3d", resolvedPaths[0]);
    }

    [Fact]
    public async Task BindAsync_ExceedsMaxTextureBindings_SkipsFurtherTexturesWithWarning()
    {
        // Arrange
        int originalLimit = PreviewPerformancePolicy.MaxTextureBindings;
        PreviewPerformancePolicy.MaxTextureBindings = 1;

        try
        {
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
                    Image = new TextureImageData { Width = 8, Height = 8 }
                })
            };

            var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

            var meshData = new O3dMeshData
            {
                MaterialSlots = [
                    new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "tex0.png" },
                    new O3dMaterialSlot { MaterialName = "Mat1", TextureReference = "tex1.png" }
                ]
            };

            // Act
            var result = await service.BindAsync(meshData, "model.o3d", "/root");

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(TextureBindingStatus.Bound, result[0].Status);
            Assert.Equal(TextureBindingStatus.TooLarge, result[1].Status);
            Assert.Single(result[1].Diagnostics);
            Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, result[1].Diagnostics[0].Code);
            Assert.Contains("Exceeded MaxTextureBindings", result[1].Diagnostics[0].Message);
        }
        finally
        {
            PreviewPerformancePolicy.MaxTextureBindings = originalLimit;
        }
    }

    [Fact]
    public async Task BindAsync_ExceedsMaxTotalTexturePixels_SkipsFurtherTexturesWithWarning()
    {
        // Arrange
        long originalLimit = PreviewPerformancePolicy.MaxTotalTexturePixels;
        PreviewPerformancePolicy.MaxTotalTexturePixels = 100;

        try
        {
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
                OnLoadAsync = (path, token) =>
                {
                    int dim = path.Contains("tex0") ? 10 : 5; // 10x10 = 100, 5x5 = 25
                    return Task.FromResult(new TextureLoadResult
                    {
                        Status = TextureLoadStatus.Success,
                        Image = new TextureImageData { Width = dim, Height = dim }
                    });
                }
            };

            var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

            var meshData = new O3dMeshData
            {
                MaterialSlots = [
                    new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "tex0.png" },
                    new O3dMaterialSlot { MaterialName = "Mat1", TextureReference = "tex1.png" }
                ]
            };

            // Act
            var result = await service.BindAsync(meshData, "model.o3d", "/root");

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(TextureBindingStatus.Bound, result[0].Status);
            Assert.Equal(TextureBindingStatus.TooLarge, result[1].Status);
            Assert.Single(result[1].Diagnostics);
            Assert.Equal(O3dDiagnosticCode.SafetyLimitExceeded, result[1].Diagnostics[0].Code);
            Assert.Contains("Total texture size exceeds MaxTotalTexturePixels", result[1].Diagnostics[0].Message);
        }
        finally
        {
            PreviewPerformancePolicy.MaxTotalTexturePixels = originalLimit;
        }
    }

    [Fact]
    public async Task BindAsync_SameTexturePath_CountsAsOneBindingAndOnePixelContribution()
    {
        // Arrange
        int originalBindingsLimit = PreviewPerformancePolicy.MaxTextureBindings;
        long originalPixelsLimit = PreviewPerformancePolicy.MaxTotalTexturePixels;
        
        // Limits: Max 1 unique texture path, Max 100 pixels
        PreviewPerformancePolicy.MaxTextureBindings = 1;
        PreviewPerformancePolicy.MaxTotalTexturePixels = 100;

        try
        {
            var fakeResolver = new FakeTextureResolver
            {
                OnResolve = (path, model, root) => new OmsiTextureReference
                {
                    TexturePath = path,
                    ResolvedPath = "/resolved/shared.png", // Same resolved path
                    Exists = true,
                    ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
                }
            };

            var fakeLoader = new FakeTextureImageLoader
            {
                OnLoadAsync = (path, token) => Task.FromResult(new TextureLoadResult
                {
                    Status = TextureLoadStatus.Success,
                    Image = new TextureImageData { Width = 10, Height = 10 } // 100 pixels
                })
            };

            var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

            var meshData = new O3dMeshData
            {
                MaterialSlots = [
                    new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "shared.png" },
                    new O3dMaterialSlot { MaterialName = "Mat1", TextureReference = "SHARED.PNG" } // Different casing
                ]
            };

            // Act
            var result = await service.BindAsync(meshData, "model.o3d", "/root");

            // Assert
            Assert.Equal(2, result.Count);
            // Both slots are successfully bound because they share the same resolved path and don't consume extra budget!
            Assert.Equal(TextureBindingStatus.Bound, result[0].Status);
            Assert.Equal(TextureBindingStatus.Bound, result[1].Status);
        }
        finally
        {
            PreviewPerformancePolicy.MaxTextureBindings = originalBindingsLimit;
            PreviewPerformancePolicy.MaxTotalTexturePixels = originalPixelsLimit;
        }
    }

    [Fact]
    public async Task BindAsync_DifferentPaths_TriggerLimitsIndependently()
    {
        // Arrange
        int originalBindingsLimit = PreviewPerformancePolicy.MaxTextureBindings;
        long originalPixelsLimit = PreviewPerformancePolicy.MaxTotalTexturePixels;

        // Limits: Max 1 unique texture path, Max 150 pixels
        PreviewPerformancePolicy.MaxTextureBindings = 1;
        PreviewPerformancePolicy.MaxTotalTexturePixels = 150;

        try
        {
            var fakeResolver = new FakeTextureResolver
            {
                OnResolve = (path, model, root) => new OmsiTextureReference
                {
                    TexturePath = path,
                    ResolvedPath = $"/resolved/{path}", // Different paths
                    Exists = true,
                    ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
                }
            };

            var fakeLoader = new FakeTextureImageLoader
            {
                OnLoadAsync = (path, token) => Task.FromResult(new TextureLoadResult
                {
                    Status = TextureLoadStatus.Success,
                    Image = new TextureImageData { Width = 10, Height = 10 } // 100 pixels
                })
            };

            var service = new MaterialTextureBindingService(fakeResolver, fakeLoader);

            var meshData = new O3dMeshData
            {
                MaterialSlots = [
                    new O3dMaterialSlot { MaterialName = "Mat0", TextureReference = "tex0.png" },
                    new O3dMaterialSlot { MaterialName = "Mat1", TextureReference = "tex1.png" }
                ]
            };

            // Act
            var result = await service.BindAsync(meshData, "model.o3d", "/root");

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(TextureBindingStatus.Bound, result[0].Status);
            // Second slot is skipped because it requires a second unique texture path which exceeds MaxTextureBindings (1)
            Assert.Equal(TextureBindingStatus.TooLarge, result[1].Status);
            Assert.Contains("Exceeded MaxTextureBindings", result[1].Diagnostics[0].Message);
        }
        finally
        {
            PreviewPerformancePolicy.MaxTextureBindings = originalBindingsLimit;
            PreviewPerformancePolicy.MaxTotalTexturePixels = originalPixelsLimit;
        }
    }
}
