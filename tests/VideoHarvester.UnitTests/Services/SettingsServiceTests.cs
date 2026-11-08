using System.Text.Json;
using VideoHarvester.Models;
using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "VideoHarvester-settings-tests-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_folder, "settings.json");

    [Fact]
    public async Task LoadCreatesDefaultSettingsFile()
    {
        var service = new SettingsService(SettingsPath);

        AppSettings settings = await service.LoadSettingsAsync();

        Assert.True(File.Exists(SettingsPath));
        Assert.True(settings.AutoStartDownload);
        Assert.Equal(1, settings.MaxConcurrentDownloads);
        Assert.Equal("Best", settings.PreferredVideoQuality);
        Assert.Equal("en", settings.Language);
    }

    [Fact]
    public async Task SaveAndLoadRoundTripsSettings()
    {
        var service = new SettingsService(SettingsPath);
        var expected = new AppSettings
        {
            DefaultDownloadPath = @"C:\Downloads",
            AutoStartDownload = false,
            ShowNotifications = false,
            MaxConcurrentDownloads = 4,
            PreferredVideoQuality = "720p",
            Language = "pl"
        };

        await service.SaveSettingsAsync(expected);
        AppSettings actual = await service.LoadSettingsAsync();

        Assert.Equal(expected.DefaultDownloadPath, actual.DefaultDownloadPath);
        Assert.Equal(expected.AutoStartDownload, actual.AutoStartDownload);
        Assert.Equal(expected.ShowNotifications, actual.ShowNotifications);
        Assert.Equal(expected.MaxConcurrentDownloads, actual.MaxConcurrentDownloads);
        Assert.Equal(expected.PreferredVideoQuality, actual.PreferredVideoQuality);
        Assert.Equal(expected.Language, actual.Language);
    }

    [Fact]
    public async Task CorruptedFileFallsBackToDefaults()
    {
        Directory.CreateDirectory(_folder);
        await File.WriteAllTextAsync(SettingsPath, "not-json");

        AppSettings settings = await new SettingsService(SettingsPath).LoadSettingsAsync();

        Assert.Equal("Best", settings.PreferredVideoQuality);
        Assert.Equal(1, settings.MaxConcurrentDownloads);
    }

    [Fact]
    public async Task SelectedLanguageSurvivesLaterSettingsSave()
    {
        var service = new SettingsService(SettingsPath);
        await service.UpdateLanguageAsync("pl");

        await service.SaveSettingsAsync(new AppSettings { Language = "en", MaxConcurrentDownloads = 3 });

        AppSettings settings = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(SettingsPath))!;
        Assert.Equal("pl", settings.Language);
        Assert.Equal(3, settings.MaxConcurrentDownloads);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }
}
