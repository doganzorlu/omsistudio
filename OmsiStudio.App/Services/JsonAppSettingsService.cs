using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public class JsonAppSettingsService : IAppSettingsService
{
    private readonly string _filePath;

    private class SettingsModel
    {
        public string? LastOmsiRoot { get; set; }
        public string? LastLanguage { get; set; }
    }

    public JsonAppSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "OmsiStudio");
        _filePath = Path.Combine(appFolder, "settings.json");
    }

    public JsonAppSettingsService(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    private async Task<SettingsModel> LoadSettingsModelAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new SettingsModel();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            var model = JsonSerializer.Deserialize<SettingsModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return model ?? new SettingsModel();
        }
        catch
        {
            return new SettingsModel();
        }
    }

    private async Task SaveSettingsModelAsync(SettingsModel model, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
            
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
            File.Move(tempPath, _filePath);
        }
        catch
        {
            // Tolerate saving failures silently
        }
    }

    public async Task<string?> GetLastOmsiRootAsync(CancellationToken cancellationToken = default)
    {
        var model = await LoadSettingsModelAsync(cancellationToken);
        return model.LastOmsiRoot;
    }

    public async Task SaveLastOmsiRootAsync(string rootDirectory, CancellationToken cancellationToken = default)
    {
        var model = await LoadSettingsModelAsync(cancellationToken);
        model.LastOmsiRoot = rootDirectory;
        await SaveSettingsModelAsync(model, cancellationToken);
    }

    public async Task<string?> GetLastLanguageAsync(CancellationToken cancellationToken = default)
    {
        var model = await LoadSettingsModelAsync(cancellationToken);
        return model.LastLanguage;
    }

    public async Task SaveLastLanguageAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        var model = await LoadSettingsModelAsync(cancellationToken);
        model.LastLanguage = cultureName;
        await SaveSettingsModelAsync(model, cancellationToken);
    }
}
