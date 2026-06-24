using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public class ProcessFileLauncherService : IFileLauncherService
{
    public async Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Folder path cannot be null or empty.", nameof(folderPath));
        }

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: '{folderPath}'");
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open directory '{folderPath}': {ex.Message}", ex);
        }

        await Task.CompletedTask;
    }
}
