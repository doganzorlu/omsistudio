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
            var lines = ReadAllLinesWithEncoding(filePath);
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

    private string[] ReadAllLinesWithEncoding(string filePath)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        var bytes = File.ReadAllBytes(filePath);

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        try
        {
            var utf8Strict = new System.Text.UTF8Encoding(false, true);
            var text = utf8Strict.GetString(bytes);
            return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }
        catch (ArgumentException)
        {
            try
            {
                var fallbackEncoding = System.Text.Encoding.GetEncoding(1254);
                var text = fallbackEncoding.GetString(bytes);
                return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            }
            catch
            {
                var fallbackEncoding = System.Text.Encoding.GetEncoding(1252);
                var text = fallbackEncoding.GetString(bytes);
                return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            }
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
        var scripts = new List<string>();
        var sounds = new List<string>();
        var collisionMeshes = new List<string>();
        var isNoCollision = false;
        var isFixed = false;

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

                    case "[script]":
                        var script = GetNextValueLine(lineList, ref i);
                        if (script != null)
                        {
                            scripts.Add(script);
                        }
                        break;

                    case "[sound]":
                        var sound = GetNextValueLine(lineList, ref i);
                        if (sound != null)
                        {
                            sounds.Add(sound);
                        }
                        break;

                    case "[collision_mesh]":
                        var colMesh = GetNextValueLine(lineList, ref i);
                        if (colMesh != null)
                        {
                            collisionMeshes.Add(colMesh);
                        }
                        break;

                    case "[nocollision]":
                        isNoCollision = true;
                        break;

                    case "[fixed]":
                        isFixed = true;
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
            Warnings = warnings,
            ScriptReferences = scripts,
            SoundReferences = sounds,
            CollisionMeshReferences = collisionMeshes,
            IsNoCollision = isNoCollision,
            IsFixed = isFixed
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
