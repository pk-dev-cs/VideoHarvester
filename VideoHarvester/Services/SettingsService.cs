using System.IO;
using System.Text.Json;
using VideoHarvester.Models;

namespace VideoHarvester.Services;

public interface ISettingsService
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    async Task UpdateLanguageAsync(string language)
    {
        var settings = await LoadSettingsAsync();
        settings.Language = language;
        await SaveSettingsAsync(settings);
    }
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private string? _selectedLanguage;

    public SettingsService(string? settingsFilePath = null)
    {
        _settingsFilePath = settingsFilePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoHarvester", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                var defaultSettings = new AppSettings();
                await WriteSettingsAsync(defaultSettings);
                return defaultSettings;
            }

            string json = await File.ReadAllTextAsync(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await _fileLock.WaitAsync();
        try
        {
            settings.Language = _selectedLanguage ?? settings.Language;
            await WriteSettingsAsync(settings);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task UpdateLanguageAsync(string language)
    {
        await _fileLock.WaitAsync();
        try
        {
            var settings = File.Exists(_settingsFilePath)
                ? JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(_settingsFilePath)) ?? new AppSettings()
                : new AppSettings();
            settings.Language = language;
            await WriteSettingsAsync(settings);
            _selectedLanguage = language;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task WriteSettingsAsync(AppSettings settings)
    {
        string temporaryPath = _settingsFilePath + ".tmp";
        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(settings, options));
        File.Move(temporaryPath, _settingsFilePath, true);
    }
}
