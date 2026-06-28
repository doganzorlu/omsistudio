using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.OmsiFormat.Scanner;

/// <summary>
/// Service that resolves texture paths referenced in O3D model files relative to the OMSI folder structure.
/// </summary>
public class OmsiTextureReferenceResolver : IOmsiTextureReferenceResolver
{
    /// <inheritdoc />
    public OmsiTextureReference Resolve(string texturePath, string modelFilePath, string sceneryObjectsRoot)
    {
        if (string.IsNullOrWhiteSpace(modelFilePath) || string.IsNullOrWhiteSpace(sceneryObjectsRoot))
        {
            return new OmsiTextureReference
            {
                TexturePath = texturePath ?? string.Empty,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.InvalidPath
            };
        }

        if (string.IsNullOrWhiteSpace(texturePath))
        {
            return new OmsiTextureReference
            {
                TexturePath = string.Empty,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.Missing
            };
        }

        var modelDir = Path.GetDirectoryName(Path.GetFullPath(modelFilePath));
        if (string.IsNullOrEmpty(modelDir))
        {
            return new OmsiTextureReference
            {
                TexturePath = texturePath,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.InvalidPath
            };
        }

        var assetDir = GetAssetFolder(modelFilePath, sceneryObjectsRoot);

        // Define allowed scopes
        var allowedScopes = new[] { modelDir, assetDir, sceneryObjectsRoot }
            .Select(Path.GetFullPath)
            .ToList();

        // Prevent path traversal in relative paths
        if (!Path.IsPathRooted(texturePath))
        {
            var segments = texturePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(s => s == ".."))
            {
                return new OmsiTextureReference
                {
                    TexturePath = texturePath,
                    ResolutionStatus = OmsiTextureReferenceResolutionStatus.InvalidPath
                };
            }
        }

        // Check if texture path is absolute
        if (Path.IsPathRooted(texturePath))
        {
            var absolutePath = Path.GetFullPath(texturePath);
            var isWithinAllowed = allowedScopes.Any(scope => IsDescendantOf(scope, absolutePath));

            if (!isWithinAllowed)
            {
                return new OmsiTextureReference
                {
                    TexturePath = texturePath,
                    ResolvedPath = absolutePath,
                    Exists = false,
                    ResolutionStatus = OmsiTextureReferenceResolutionStatus.InvalidPath
                };
            }

            var exists = File.Exists(absolutePath);
            return new OmsiTextureReference
            {
                TexturePath = texturePath,
                ResolvedPath = absolutePath,
                Exists = exists,
                ResolutionStatus = exists ? OmsiTextureReferenceResolutionStatus.Resolved : OmsiTextureReferenceResolutionStatus.Missing
            };
        }

        // Normalize slashes for relative path search
        var normalizedTexturePath = texturePath.Replace('\\', '/');

        // Construct search candidates with base directories in specified order
        var searchBases = new List<string>
        {
            modelDir,
            Path.Combine(modelDir, "texture"),
            Path.Combine(modelDir, "Texture"),
            Path.Combine(modelDir, "Textures"),
            assetDir,
            Path.Combine(assetDir, "texture"),
            Path.Combine(assetDir, "Texture"),
            Path.Combine(assetDir, "Textures"),
            sceneryObjectsRoot
        };

        var firstValidScopeCandidate = (string?)null;

        foreach (var baseDir in searchBases)
        {
            // Case-insensitive resolution
            var resolvedPath = ResolveCaseInsensitive(baseDir, normalizedTexturePath);
            if (resolvedPath == null)
            {
                // If not found in file system, let's still evaluate the structural path to check traversal rules
                try
                {
                    resolvedPath = Path.GetFullPath(Path.Combine(baseDir, normalizedTexturePath));
                }
                catch
                {
                    continue;
                }
            }

            var isWithinAllowed = allowedScopes.Any(scope => IsDescendantOf(scope, resolvedPath));
            if (isWithinAllowed)
            {
                if (firstValidScopeCandidate == null)
                {
                    firstValidScopeCandidate = resolvedPath;
                }

                if (File.Exists(resolvedPath))
                {
                    return new OmsiTextureReference
                    {
                        TexturePath = texturePath,
                        ResolvedPath = resolvedPath,
                        Exists = true,
                        ResolutionStatus = OmsiTextureReferenceResolutionStatus.Resolved
                    };
                }
            }
        }

        // If we get here, no existing file was found.
        if (firstValidScopeCandidate != null)
        {
            return new OmsiTextureReference
            {
                TexturePath = texturePath,
                ResolvedPath = firstValidScopeCandidate,
                Exists = false,
                ResolutionStatus = OmsiTextureReferenceResolutionStatus.Missing
            };
        }

        // If there was any traversal violation
        return new OmsiTextureReference
        {
            TexturePath = texturePath,
            ResolvedPath = null,
            Exists = false,
            ResolutionStatus = OmsiTextureReferenceResolutionStatus.InvalidPath
        };
    }

    private string GetAssetFolder(string modelFilePath, string sceneryObjectsRoot)
    {
        try
        {
            var modelFull = Path.GetFullPath(modelFilePath);
            var rootFull = Path.GetFullPath(sceneryObjectsRoot);
            var relative = Path.GetRelativePath(rootFull, modelFull);
            if (!relative.StartsWith("..") && !Path.IsPathRooted(relative))
            {
                var parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    return Path.Combine(rootFull, parts[0]);
                }
            }
        }
        catch
        {
        }

        var dir = Path.GetDirectoryName(modelFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.Equals("model", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(dir);
                if (!string.IsNullOrEmpty(parent))
                {
                    return parent;
                }
            }
            var parent2 = Path.GetDirectoryName(dir);
            if (!string.IsNullOrEmpty(parent2))
            {
                return parent2;
            }
            return dir;
        }
        return sceneryObjectsRoot;
    }

    private bool IsDescendantOf(string parent, string child)
    {
        try
        {
            var parentFull = Path.GetFullPath(parent).Replace('\\', '/').TrimEnd('/');
            var childFull = Path.GetFullPath(child).Replace('\\', '/').TrimEnd('/');

            if (parentFull.Equals(childFull, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return childFull.StartsWith(parentFull + "/", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string? ResolveCaseInsensitive(string basePath, string relativePath)
    {
        try
        {
            var currentPath = Path.GetFullPath(basePath);
            var parts = relativePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                if (part == "..")
                {
                    return null;
                }
                if (part == ".")
                {
                    continue;
                }

                if (!Directory.Exists(currentPath))
                {
                    return null;
                }

                var matched = Directory.EnumerateFileSystemEntries(currentPath)
                    .FirstOrDefault(entry => Path.GetFileName(entry).Equals(part, StringComparison.OrdinalIgnoreCase));

                if (matched == null)
                {
                    return null;
                }
                currentPath = matched;
            }

            return currentPath;
        }
        catch
        {
            return null;
        }
    }
}
