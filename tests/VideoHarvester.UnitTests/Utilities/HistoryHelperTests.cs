using VideoHarvester.Models;
using VideoHarvester.Services;
using VideoHarvester.Utilities;

namespace VideoHarvester.UnitTests.Utilities;

public class HistoryHelperTests
{
    [Fact]
    public async Task GetStatisticsAggregatesHistory()
    {
        var history = new StubHistoryService
        {
            Downloads =
            [
                CreateDownload("Completed", 100),
                CreateDownload("Completed", 200),
                CreateDownload("Failed", null),
                CreateDownload("Started", 50)
            ]
        };

        var result = await HistoryHelper.GetStatistics(history);

        Assert.Equal(4, result.Total);
        Assert.Equal(2, result.Completed);
        Assert.Equal(1, result.Failed);
        Assert.Equal(350, result.TotalSize);
    }

    private static DownloadHistory CreateDownload(string status, long? size) => new()
    {
        SourceUrl = "url",
        Status = status,
        FileSize = size
    };

    private sealed class StubHistoryService : IDownloadHistoryService
    {
        public List<DownloadHistory> Downloads { get; init; } = [];
        public Task<List<DownloadHistory>> GetAllDownloadsAsync() => Task.FromResult(Downloads);
        public Task InitializeDatabaseAsync() => Task.CompletedTask;
        public Task<int> AddDownloadAsync(DownloadHistory download) => Task.FromResult(0);
        public Task UpdateDownloadAsync(DownloadHistory download) => Task.CompletedTask;
        public Task<DownloadHistory?> GetDownloadByIdAsync(int id) => Task.FromResult<DownloadHistory?>(null);
        public Task<List<DownloadHistory>> GetDownloadsByStatusAsync(string status) => Task.FromResult(new List<DownloadHistory>());
        public Task DeleteDownloadAsync(int id) => Task.CompletedTask;
        public Task ClearDownloadsAsync() => Task.CompletedTask;
    }
}
