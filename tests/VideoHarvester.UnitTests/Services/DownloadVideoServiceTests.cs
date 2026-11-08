using VideoHarvester.Models;
using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.Services;

public class DownloadVideoServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "VideoHarvester-download-service-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PreCancelledDownloadCreatesCancelledHistoryEntry()
    {
        var history = new RecordingHistoryService();
        var service = new DownloadVideoService(history);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DownloadVideo(CreateVideo(VideoSource.Wistia), cancellation.Token));

        Assert.Single(history.Added);
        DownloadHistory updated = Assert.Single(history.Updated);
        Assert.Equal("Cancelled", updated.Status);
        Assert.NotNull(updated.FinishedAt);
    }

    [Fact]
    public async Task UnsupportedSourceCreatesFailedHistoryEntry()
    {
        var history = new RecordingHistoryService();
        var service = new DownloadVideoService(history);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.DownloadVideo(CreateVideo((VideoSource)999)));

        DownloadHistory updated = Assert.Single(history.Updated);
        Assert.Equal("Failed", updated.Status);
        Assert.Contains("Unsupported video source", updated.ErrorMessage);
    }

    private Video CreateVideo(VideoSource source) => new()
    {
        VideoId = "video",
        Source = source,
        DownloadDirectory = _folder
    };

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    private sealed class RecordingHistoryService : IDownloadHistoryService
    {
        public List<DownloadHistory> Added { get; } = [];
        public List<DownloadHistory> Updated { get; } = [];
        public Task<int> AddDownloadAsync(DownloadHistory download)
        {
            Added.Add(download);
            return Task.FromResult(1);
        }
        public Task UpdateDownloadAsync(DownloadHistory download)
        {
            Updated.Add(download);
            return Task.CompletedTask;
        }
        public Task InitializeDatabaseAsync() => Task.CompletedTask;
        public Task<DownloadHistory?> GetDownloadByIdAsync(int id) => Task.FromResult<DownloadHistory?>(null);
        public Task<List<DownloadHistory>> GetAllDownloadsAsync() => Task.FromResult(new List<DownloadHistory>());
        public Task<List<DownloadHistory>> GetDownloadsByStatusAsync(string status) => Task.FromResult(new List<DownloadHistory>());
        public Task DeleteDownloadAsync(int id) => Task.CompletedTask;
        public Task ClearDownloadsAsync() => Task.CompletedTask;
    }
}
