using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public sealed class JsonScanCacheService : IScanCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private string GetCacheFilePath(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return string.Empty;
        }

        try
        {
            var normalized = rootDirectory.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            var hashStr = Convert.ToHexString(hashBytes).ToLowerInvariant();
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "OmsiStudio");
            return Path.Combine(dir, $"scan-cache-{hashStr}.json");
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<OmsiScanCacheEntry?> GetAsync(string rootDirectory, CancellationToken cancellationToken = default)
    {
        var path = GetCacheFilePath(rootDirectory);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<OmsiScanCacheEntry>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(OmsiScanCacheEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.RootDirectory))
        {
            return;
        }

        var path = GetCacheFilePath(entry.RootDirectory);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken);
        }
        catch
        {
            // Do not crash
        }
    }

    public Task DeleteAsync(string rootDirectory, CancellationToken cancellationToken = default)
    {
        var path = GetCacheFilePath(rootDirectory);
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Do not crash
            }
        }
        return Task.CompletedTask;
    }
}
