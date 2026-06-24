using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OmsiStudio.OmsiFormat.Sco;

public sealed class ScoParser
{
    private static readonly string[] TextureExtensions = { ".bmp", ".dds", ".png", ".jpg", ".jpeg", ".tga" };

    public ScoFile ParseFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new ScoFile { FilePath = filePath };
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            return Parse(filePath, lines);
        }
        catch (Exception ex)
        {
            return new ScoFile
            {
                FilePath = filePath,
                Warnings = new[] { $"Failed to read file: {ex.Message}" }
            };
        }
    }

    public ScoFile Parse(string filePath, IEnumerable<string> lines)
    {
        var friendlyName = string.Empty;
        var description = string.Empty;
        var groups = new List<string>();
        var meshes = new List<ScoMeshReference>();
        var textureReferences = new List<string>();
        var warnings = new List<string>();

        var lineList = lines
            .Select(l => l.Trim())
            .ToList();

        for (int i = 0; i < lineList.Count; i++)
        {
            var line = lineList[i];
            if (string.IsNullOrEmpty(line) || line.StartsWith("'"))
            {
                continue;
            }

            // Check if this line is an obvious texture reference
            foreach (var ext in TextureExtensions)
            {
                if (line.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    if (!textureReferences.Contains(line, StringComparer.OrdinalIgnoreCase))
                    {
                        textureReferences.Add(line);
                    }
                    break;
                }
            }

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                var keyword = line.ToLowerInvariant();
                switch (keyword)
                {
                    case "[friendlyname]":
                        var fn = GetNextValueLine(lineList, ref i);
                        if (fn != null)
                        {
                            friendlyName = fn;
                        }
                        break;

                    case "[description]":
                        var desc = GetNextValueLine(lineList, ref i);
                        if (desc != null)
                        {
                            description = desc;
                        }
                        break;

                    case "[groups]":
                        var countStr = GetNextValueLine(lineList, ref i);
                        if (countStr != null && int.TryParse(countStr, out int count))
                        {
                            for (int j = 0; j < count; j++)
                            {
                                var grp = GetNextValueLine(lineList, ref i);
                                if (grp != null)
                                {
                                    groups.Add(grp);
                                }
                                else
                                {
                                    warnings.Add("Groups block declared fewer entries than count specified.");
                                    break;
                                }
                            }
                        }
                        else if (countStr != null)
                        {
                            warnings.Add($"Malformed [groups] count: '{countStr}'");
                        }
                        break;

                    case "[mesh]":
                        var mesh = GetNextValueLine(lineList, ref i);
                        if (mesh != null)
                        {
                            meshes.Add(new ScoMeshReference(mesh));
                        }
                        break;
                }
            }
        }

        return new ScoFile
        {
            FilePath = filePath,
            FriendlyName = friendlyName,
            Description = description,
            Groups = groups,
            Meshes = meshes,
            TextureReferences = textureReferences,
            Warnings = warnings
        };
    }

    private string? GetNextValueLine(List<string> lines, ref int index)
    {
        while (index + 1 < lines.Count)
        {
            index++;
            var line = lines[index];
            if (string.IsNullOrEmpty(line) || line.StartsWith("'"))
            {
                continue;
            }
            return line;
        }
        return null;
    }
}
