using System;
using System.IO;
using System.Linq;
using OmsiStudio.Core.Assets;
using OmsiStudio.Core.Services;

namespace OmsiStudio.OmsiFormat.Scanner;

public class OmsiModelReferenceResolver : IOmsiModelReferenceResolver
{
    public OmsiModelReference Resolve(string omsiRoot, string scoFilePath, string meshPath)
    {
        if (string.IsNullOrWhiteSpace(scoFilePath))
        {
            return new OmsiModelReference(meshPath, string.Empty, false, OmsiModelReferenceResolutionStatus.InvalidPath);
        }

        var scoDir = Path.GetDirectoryName(Path.GetFullPath(scoFilePath));
        if (string.IsNullOrEmpty(scoDir))
        {
            return new OmsiModelReference(meshPath, string.Empty, false, OmsiModelReferenceResolutionStatus.InvalidPath);
        }

        var sceneryObjectsDir = OmsiDirectoryHelper.GetSceneryObjectsDir(omsiRoot);

        // Normalize slashes in meshPath for standard path operations
        var normalizedMeshPath = meshPath.Replace('\\', '/');

        // Check Candidates
        // Candidate 1: Direct relative path
        var candidate1FullPath = Path.GetFullPath(Path.Combine(scoDir, normalizedMeshPath));

        // Candidate 2: Under Model subfolder of the scoDir
        var candidate2FullPath = Path.GetFullPath(Path.Combine(scoDir, "Model", normalizedMeshPath));

        // Perform Traversal Validation for both candidates
        var c1Valid = IsWithinAllowedScope(scoDir, sceneryObjectsDir, candidate1FullPath);
        var c2Valid = IsWithinAllowedScope(scoDir, sceneryObjectsDir, candidate2FullPath);

        if (!c1Valid && !c2Valid)
        {
            // If both escape allowed scope, it is a traversal violation
            return new OmsiModelReference(meshPath, candidate1FullPath, false, OmsiModelReferenceResolutionStatus.InvalidPath);
        }

        // Search filesystem (case-insensitive)
        if (c1Valid)
        {
            var resolvedC1 = ResolveCaseInsensitive(scoDir, normalizedMeshPath);
            if (resolvedC1 != null && File.Exists(resolvedC1))
            {
                return new OmsiModelReference(meshPath, resolvedC1, true, OmsiModelReferenceResolutionStatus.Resolved);
            }
        }

        if (c2Valid)
        {
            var resolvedC2 = ResolveCaseInsensitive(Path.Combine(scoDir, "Model"), normalizedMeshPath);
            if (resolvedC2 != null && File.Exists(resolvedC2))
            {
                return new OmsiModelReference(meshPath, resolvedC2, true, OmsiModelReferenceResolutionStatus.Resolved);
            }
        }

        // If not found, check which one was valid to return as Missing
        if (c1Valid)
        {
            return new OmsiModelReference(meshPath, candidate1FullPath, false, OmsiModelReferenceResolutionStatus.Missing);
        }
        else
        {
            return new OmsiModelReference(meshPath, candidate2FullPath, false, OmsiModelReferenceResolutionStatus.Missing);
        }
    }

    private bool IsWithinAllowedScope(string scoDir, string sceneryObjectsDir, string fullPath)
    {
        // Must be a descendant of either scoDir OR sceneryObjectsDir
        return IsDescendantOf(scoDir, fullPath) || IsDescendantOf(sceneryObjectsDir, fullPath);
    }

    private bool IsDescendantOf(string parent, string child)
    {
        try
        {
            var parentFull = Path.GetFullPath(parent);
            var childFull = Path.GetFullPath(child);
            var relative = Path.GetRelativePath(parentFull, childFull);
            return !relative.StartsWith("..") && !Path.IsPathRooted(relative);
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
                    currentPath = Path.GetDirectoryName(currentPath);
                    if (currentPath == null) return null;
                    continue;
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
