using Microsoft.Extensions.DependencyInjection;
using VideoHarvester;
using VideoHarvester.Models;
using VideoHarvester.Services;

internal static class UpdatePreview
{
    public static void Run(bool unfinishedQueue)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAppUpdateService, PreviewUpdates>();
        services.AddSingleton<ISettingsService, PreviewSettings>();
        services.AddSingleton<IDownloadHistoryService, PreviewHistory>();
        services.AddSingleton<IDownloadVideoService, PreviewDownloads>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        var provider = services.BuildServiceProvider();
        var app = new App(provider);
        app.InitializeComponent();
        if (unfinishedQueue)
            provider.GetRequiredService<MainWindowViewModel>().DownloadQueue.Add(new Video { VideoId = "preview", Source = VideoSource.Wistia, Status = "Paused" });
        app.Run();
    }

    private sealed class PreviewUpdates : IAppUpdateService
    {
        public bool IsInstalled => true;
        public bool HasDownloadedUpdate { get; private set; }
        public Task<string?> CheckAsync() => Task.FromResult<string?>("0.4.0");
        public async Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
        {
            progress.Report(50);
            await Task.Delay(150, cancellationToken);
            progress.Report(100);
            HasDownloadedUpdate = true;
        }
        public void ApplyAndRestart() => throw new InvalidOperationException("Preview mode never installs an update.");
    }

    private sealed class PreviewSettings : ISettingsService
    {
        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(new AppSettings { AutoStartDownload = false });
        public Task SaveSettingsAsync(AppSettings settings) => Task.CompletedTask;
    }

    private sealed class PreviewDownloads : IDownloadVideoService
    {
        public Task DownloadVideo(Video video, CancellationToken cancellationToken = default) => throw new NotSupportedException("Downloads are disabled in the update preview.");
    }

    private sealed class PreviewHistory : IDownloadHistoryService
    {
        public Task InitializeDatabaseAsync() => Task.CompletedTask;
        public Task<int> AddDownloadAsync(DownloadHistory download) => Task.FromResult(1);
        public Task UpdateDownloadAsync(DownloadHistory download) => Task.CompletedTask;
        public Task<DownloadHistory?> GetDownloadByIdAsync(int id) => Task.FromResult<DownloadHistory?>(null);
        public Task<List<DownloadHistory>> GetAllDownloadsAsync() => Task.FromResult(new List<DownloadHistory>());
        public Task<List<DownloadHistory>> GetDownloadsByStatusAsync(string status) => GetAllDownloadsAsync();
        public Task DeleteDownloadAsync(int id) => Task.CompletedTask;
        public Task ClearDownloadsAsync() => Task.CompletedTask;
    }
}
