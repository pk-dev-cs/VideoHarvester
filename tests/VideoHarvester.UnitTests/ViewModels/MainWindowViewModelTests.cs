using VideoHarvester.Models;
using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.ViewModels;

public class MainWindowViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "VideoHarvester-main-vm-tests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(VideoSource.YouTube, "https://youtu.be/abc123", "https://youtu.be/abc123", "mkv")]
    [InlineData(VideoSource.YouTubeWav, "https://youtube.com/watch?v=abc123", "https://youtube.com/watch?v=abc123", "wav")]
    [InlineData(VideoSource.Cda, "https://cda.pl/video/2882914921", "2882914921", "mp4")]
    public void AddToQueueNormalizesReferenceAndCapturesSettings(VideoSource source, string input, string expectedReference, string extension)
    {
        MainWindowViewModel viewModel = CreateViewModel(out _);
        viewModel.SelectedSource = source;
        viewModel.VideoId = input;

        viewModel.AddToQueueCommand.Execute(null);

        Video video = Assert.Single(viewModel.DownloadQueue);
        Assert.Equal(expectedReference, video.VideoId);
        Assert.Equal(source, video.Source);
        Assert.Equal("Ready", video.Status);
        Assert.Equal("720p", video.PreferredQuality);
        Assert.Equal(_folder, video.DownloadDirectory);
        Assert.EndsWith('.' + extension, video.FilePath);
    }

    [Fact]
    public void AddToQueueExtractsWistiaReference()
    {
        MainWindowViewModel viewModel = CreateViewModel(out _);
        viewModel.SelectedSource = VideoSource.Wistia;
        viewModel.VideoId = "<a href=\"https://example.test?wvideo=wistia123\">Watch</a>";

        viewModel.AddToQueueCommand.Execute(null);

        Assert.Equal("wistia123", Assert.Single(viewModel.DownloadQueue).VideoId);
    }

    [Fact]
    public void RemoveFromQueueRemovesSelectedVideo()
    {
        MainWindowViewModel viewModel = CreateViewModel(out _);
        viewModel.SelectedSource = VideoSource.Cda;
        viewModel.VideoId = "2882914921";
        viewModel.AddToQueueCommand.Execute(null);
        Video video = Assert.Single(viewModel.DownloadQueue);

        viewModel.RemoveFromQueueCommand.Execute(video);

        Assert.Empty(viewModel.DownloadQueue);
    }

    [Fact]
    public void StopMarksIdleDownloadAsCancelled()
    {
        MainWindowViewModel viewModel = CreateViewModel(out _);
        var video = new Video { VideoId = "id", Source = VideoSource.Wistia, Status = "Ready" };

        viewModel.StopDownloadCommand.Execute(video);

        Assert.Equal("Cancelled", video.Status);
    }

    [Fact]
    public async Task LoadAndRemoveHistoryDelegateToService()
    {
        MainWindowViewModel viewModel = CreateViewModel(out RecordingHistoryService history);
        history.Downloads.Add(new DownloadHistory { Id = 7, SourceUrl = "url", Status = "Downloaded" });

        await viewModel.LoadHistoryCommand.ExecuteAsync(null);
        DownloadHistory item = Assert.Single(viewModel.DownloadHistory);
        await viewModel.RemoveFromHistoryCommand.ExecuteAsync(item);

        Assert.Empty(viewModel.DownloadHistory);
        Assert.Equal([7], history.DeletedIds);
    }

    [Fact]
    public void AvailableSourcesContainsEverySource()
    {
        MainWindowViewModel viewModel = CreateViewModel(out _);
        Assert.Equal(Enum.GetValues<VideoSource>(), viewModel.AvailableSources);
    }

    private MainWindowViewModel CreateViewModel(out RecordingHistoryService history)
    {
        var settingsService = new FakeSettingsService(new AppSettings
        {
            AutoStartDownload = false,
            DefaultDownloadPath = _folder,
            PreferredVideoQuality = "720p",
            MaxConcurrentDownloads = 2
        });
        var settingsViewModel = new SettingsViewModel(settingsService);
        history = new RecordingHistoryService();
        return new MainWindowViewModel(new NoOpDownloadService(), history, settingsService, settingsViewModel);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    private sealed class FakeSettingsService(AppSettings settings) : ISettingsService
    {
        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(settings);
        public Task SaveSettingsAsync(AppSettings value) => Task.CompletedTask;
    }

    private sealed class NoOpDownloadService : IDownloadVideoService
    {
        public Task DownloadVideo(Video video, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingHistoryService : IDownloadHistoryService
    {
        public List<DownloadHistory> Downloads { get; } = [];
        public List<int> DeletedIds { get; } = [];
        public Task<List<DownloadHistory>> GetAllDownloadsAsync() => Task.FromResult(Downloads.ToList());
        public Task DeleteDownloadAsync(int id) { DeletedIds.Add(id); return Task.CompletedTask; }
        public Task InitializeDatabaseAsync() => Task.CompletedTask;
        public Task<int> AddDownloadAsync(DownloadHistory download) => Task.FromResult(1);
        public Task UpdateDownloadAsync(DownloadHistory download) => Task.CompletedTask;
        public Task<DownloadHistory?> GetDownloadByIdAsync(int id) => Task.FromResult<DownloadHistory?>(null);
        public Task<List<DownloadHistory>> GetDownloadsByStatusAsync(string status) => Task.FromResult(new List<DownloadHistory>());
        public Task ClearDownloadsAsync() { Downloads.Clear(); return Task.CompletedTask; }
    }
}
