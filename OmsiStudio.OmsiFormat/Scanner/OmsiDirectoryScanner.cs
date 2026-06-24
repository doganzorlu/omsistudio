using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OmsiStudio.Core.Services;

namespace OmsiStudio.OmsiFormat.Scanner;

public class OmsiDirectoryScanner : IOmsiDirectoryScanner
{
    public async IAsyncEnumerable<string> FindScoFilesAsync(
        string omsiRootDirectory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(omsiRootDirectory) || !Directory.Exists(omsiRootDirectory))
        {
            yield break;
        }

        var sceneryObjectsDir = OmsiDirectoryHelper.GetSceneryObjectsDir(omsiRootDirectory);

        if (!Directory.Exists(sceneryObjectsDir))
        {
            yield break;
        }

        var files = Directory.EnumerateFiles(sceneryObjectsDir, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(Path.GetExtension(file), ".sco", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
                await Task.Yield();
            }
        }
    }
}
