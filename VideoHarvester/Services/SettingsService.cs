using System.IO;
using System.Text.Json;
using VideoHarvester.Models;

namespace VideoHarvester.Services;

public interface ISettingsService
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;

    public SettingsService()
    {
        string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoHarvester");
        Directory.CreateDirectory(appDataPath);
        _settingsFilePath = Path.Combine(appDataPath, "settings.json");
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                var defaultSettings = new AppSettings();
                await SaveSettingsAsync(defaultSettings);
                return defaultSettings;
            }

            string json = await File.ReadAllTextAsync(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch
        {
            // Handle error silently or log
        }
    }
}
