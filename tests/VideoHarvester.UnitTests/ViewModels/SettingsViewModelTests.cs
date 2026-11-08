using VideoHarvester.Models;
using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.ViewModels;

public class SettingsViewModelTests
{
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(3, 3)]
    [InlineData(6, 5)]
    public void ConcurrentDownloadsAreClamped(int requested, int expected)
    {
        var viewModel = new SettingsViewModel(new FakeSettingsService());
        viewModel.MaxConcurrentDownloads = requested;
        Assert.Equal(expected, viewModel.MaxConcurrentDownloads);
    }

    [Fact]
    public async Task LoadsPersistedSettings()
    {
        var service = new FakeSettingsService
        {
            Settings = new AppSettings
            {
                DefaultDownloadPath = @"C:\Downloads",
                AutoStartDownload = false,
                ShowNotifications = false,
                MaxConcurrentDownloads = 4,
                PreferredVideoQuality = "720p"
            }
        };

        var viewModel = new SettingsViewModel(service);
        await WaitUntil(() => viewModel.DefaultDownloadPath == @"C:\Downloads");

        Assert.False(viewModel.AutoStartDownload);
        Assert.False(viewModel.ShowNotifications);
        Assert.Equal(4, viewModel.MaxConcurrentDownloads);
        Assert.Equal("720p", viewModel.PreferredVideoQuality);
    }

    [Fact]
    public async Task SavePersistsCurrentValues()
    {
        var service = new FakeSettingsService();
        var viewModel = new SettingsViewModel(service);
        await WaitUntil(() => service.LoadCalls > 0);
        viewModel.DefaultDownloadPath = @"C:\New";
        viewModel.AutoStartDownload = false;
        viewModel.ShowNotifications = false;
        viewModel.MaxConcurrentDownloads = 2;
        viewModel.PreferredVideoQuality = "480p";

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.NotNull(service.SavedSettings);
        Assert.Equal(@"C:\New", service.SavedSettings.DefaultDownloadPath);
        Assert.False(service.SavedSettings.AutoStartDownload);
        Assert.Equal(2, service.SavedSettings.MaxConcurrentDownloads);
        Assert.Equal("480p", service.SavedSettings.PreferredVideoQuality);
        Assert.False(viewModel.IsSaving);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; init; } = new();
        public AppSettings? SavedSettings { get; private set; }
        public int LoadCalls { get; private set; }

        public Task<AppSettings> LoadSettingsAsync()
        {
            LoadCalls++;
            return Task.FromResult(Settings);
        }

        public Task SaveSettingsAsync(AppSettings settings)
        {
            SavedSettings = settings;
            return Task.CompletedTask;
        }
    }
}
