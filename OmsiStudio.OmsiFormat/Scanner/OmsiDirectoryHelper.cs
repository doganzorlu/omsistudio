using System;
using System.IO;
using System.Linq;

namespace OmsiStudio.OmsiFormat.Scanner;

public static class OmsiDirectoryHelper
{
    public static string GetSceneryObjectsDir(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return Path.Combine(rootDirectory, "Sceneryobjects");
        }

        try
        {
            var dir = Directory.EnumerateDirectories(rootDirectory)
                .FirstOrDefault(d => Path.GetFileName(d).Equals("sceneryobjects", StringComparison.OrdinalIgnoreCase));
            
            if (dir != null)
            {
                return dir;
            }
        }
        catch (Exception)
        {
            // Ignore exception here; caller will check if the returned directory exists
        }

        return Path.Combine(rootDirectory, "Sceneryobjects");
    }
}
