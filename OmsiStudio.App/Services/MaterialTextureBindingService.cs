using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;
using OmsiStudio.App.Services.Rendering;

namespace OmsiStudio.App.Services;

/// <summary>
/// A concrete implementation of <see cref="IMaterialTextureBindingService"/> that maps O3D material slots to decoded texture images.
/// </summary>
public class MaterialTextureBindingService : IMaterialTextureBindingService
{
    private readonly IOmsiTextureReferenceResolver _textureResolver;
    private readonly ITextureImageCacheService _imageCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialTextureBindingService"/> class.
    /// </summary>
    /// <param name="textureResolver">The texture reference path resolver.</param>
    /// <param name="imageCache">The texture cache service.</param>
    public MaterialTextureBindingService(
        IOmsiTextureReferenceResolver textureResolver,
        ITextureImageCacheService imageCache)
    {
        _textureResolver = textureResolver ?? throw new ArgumentNullException(nameof(textureResolver));
        _imageCache = imageCache ?? throw new ArgumentNullException(nameof(imageCache));
    }

    /// <summary>
    /// Backward-compatible constructor for testing.
    /// </summary>
    public MaterialTextureBindingService(
        IOmsiTextureReferenceResolver textureResolver,
        ITextureImageLoader imageLoader)
        : this(textureResolver, new TextureImageCacheService(imageLoader))
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaterialTextureBinding>> BindAsync(
        O3dMeshData meshData,
        string modelFilePath,
        string sceneryObjectsRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (meshData == null)
        {
            throw new ArgumentNullException(nameof(meshData));
        }

        var bindings = new List<MaterialTextureBinding>();

        for (int i = 0; i < meshData.MaterialSlots.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var slot = meshData.MaterialSlots[i];
            var materialIndex = i;
            var materialName = slot.MaterialName ?? string.Empty;

            // 1. Empty or Null Texture Reference Case
            if (string.IsNullOrWhiteSpace(slot.TextureReference))
            {
                bindings.Add(new MaterialTextureBinding
                {
                    MaterialIndex = materialIndex,
                    MaterialName = materialName,
                    TextureReference = string.Empty,
                    Status = TextureBindingStatus.Missing,
                    Diagnostics = [new O3dDiagnostic
                    {
                        Severity = O3dDiagnosticSeverity.Warning,
                        Message = $"[Material {materialIndex} - {materialName}] Material slot has no texture reference."
                    }]
                });
                continue;
            }

            var textureRef = slot.TextureReference;

            // 2. Resolve Texture Reference Path
            var resolvedTexture = _textureResolver.Resolve(textureRef, modelFilePath, sceneryObjectsRoot);

            if (resolvedTexture.ResolutionStatus == OmsiTextureReferenceResolutionStatus.Missing)
            {
                bindings.Add(new MaterialTextureBinding
                {
                    MaterialIndex = materialIndex,
                    MaterialName = materialName,
                    TextureReference = textureRef,
                    ResolvedTexture = resolvedTexture,
                    Status = TextureBindingStatus.Missing,
                    Diagnostics = [new O3dDiagnostic
                    {
                        Severity = O3dDiagnosticSeverity.Warning,
                        Code = O3dDiagnosticCode.FileNotFound,
                        Message = $"[Material {materialIndex} - {materialName}] Texture file is missing: {textureRef}"
                    }]
                });
                continue;
            }

            if (resolvedTexture.ResolutionStatus == OmsiTextureReferenceResolutionStatus.InvalidPath)
            {
                bindings.Add(new MaterialTextureBinding
                {
                    MaterialIndex = materialIndex,
                    MaterialName = materialName,
                    TextureReference = textureRef,
                    ResolvedTexture = resolvedTexture,
                    Status = TextureBindingStatus.Invalid,
                    Diagnostics = [new O3dDiagnostic
                    {
                        Severity = O3dDiagnosticSeverity.Error,
                        Code = O3dDiagnosticCode.InvalidPath,
                        Message = $"[Material {materialIndex} - {materialName}] Texture path is invalid or violates path traversal constraints: {textureRef}"
                    }]
                });
                continue;
            }

            // 3. Load & Decode Texture
            if (resolvedTexture.ResolutionStatus == OmsiTextureReferenceResolutionStatus.Resolved && resolvedTexture.ResolvedPath != null)
            {
                var loaderResult = await _imageCache.GetOrLoadAsync(resolvedTexture.ResolvedPath, cancellationToken);

                var diagnostics = new List<O3dDiagnostic>();
                foreach (var diag in loaderResult.Diagnostics)
                {
                    diagnostics.Add(new O3dDiagnostic
                    {
                        Severity = diag.Severity,
                        Code = diag.Code,
                        ByteOffset = diag.ByteOffset,
                        Context = diag.Context,
                        Message = $"[Material {materialIndex} - {materialName}] {diag.Message}"
                    });
                }

                var bindingStatus = loaderResult.Status switch
                {
                    TextureLoadStatus.Success => TextureBindingStatus.Bound,
                    TextureLoadStatus.UnsupportedFormat => TextureBindingStatus.Unsupported,
                    TextureLoadStatus.Invalid => TextureBindingStatus.Invalid,
                    TextureLoadStatus.TooLarge => TextureBindingStatus.TooLarge,
                    TextureLoadStatus.Failed => TextureBindingStatus.Failed,
                    _ => TextureBindingStatus.Unknown
                };

                bindings.Add(new MaterialTextureBinding
                {
                    MaterialIndex = materialIndex,
                    MaterialName = materialName,
                    TextureReference = textureRef,
                    ResolvedTexture = resolvedTexture,
                    Image = loaderResult.Image,
                    Status = bindingStatus,
                    Diagnostics = diagnostics
                });
            }
            else
            {
                // Fallback status for unknown outcomes
                bindings.Add(new MaterialTextureBinding
                {
                    MaterialIndex = materialIndex,
                    MaterialName = materialName,
                    TextureReference = textureRef,
                    ResolvedTexture = resolvedTexture,
                    Status = TextureBindingStatus.Unknown,
                    Diagnostics = [new O3dDiagnostic
                    {
                        Severity = O3dDiagnosticSeverity.Error,
                        Message = $"[Material {materialIndex} - {materialName}] Failed to resolve texture reference: {textureRef}"
                    }]
                });
            }
        }

        return bindings;
    }
}
