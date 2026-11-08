using VideoHarvester.Models;
using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.Services;

public class DownloadHistoryServiceTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "VideoHarvester-history-tests-" + Guid.NewGuid().ToString("N"));
    private DownloadHistoryService _service = null!;

    public async Task InitializeAsync()
    {
        string databasePath = Path.Combine(_folder, "downloads.db");
        _service = new DownloadHistoryService(databasePath, Path.Combine(_folder, "legacy.db"));
        await _service.InitializeDatabaseAsync();
    }

    [Fact]
    public async Task AddAndGetRoundTripsAllFields()
    {
        DateTime created = DateTime.UtcNow.AddMinutes(-5);
        DateTime finished = DateTime.UtcNow;
        var expected = new DownloadHistory
        {
            SourceUrl = "https://example.test/video",
            Title = "Title",
            LocalPath = @"C:\video.mp4",
            Status = "Downloaded",
            CreatedAt = created,
            FinishedAt = finished,
            FileSize = 12345,
            Format = "mp4",
            Quality = "720p",
            ThumbnailData = [1, 2, 3],
            ErrorMessage = null
        };

        int id = await _service.AddDownloadAsync(expected);
        DownloadHistory actual = Assert.IsType<DownloadHistory>(await _service.GetDownloadByIdAsync(id));

        Assert.Equal(expected.SourceUrl, actual.SourceUrl);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.LocalPath, actual.LocalPath);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        Assert.Equal(expected.FinishedAt, actual.FinishedAt);
        Assert.Equal(expected.FileSize, actual.FileSize);
        Assert.Equal(expected.Format, actual.Format);
        Assert.Equal(expected.Quality, actual.Quality);
        Assert.Equal(expected.ThumbnailData, actual.ThumbnailData);
    }

    [Fact]
    public async Task UpdatePersistsChangedFields()
    {
        var download = CreateDownload("Started", DateTime.UtcNow);
        download.Id = await _service.AddDownloadAsync(download);
        download.Status = "Failed";
        download.ErrorMessage = "Failure";
        download.FinishedAt = DateTime.UtcNow;

        await _service.UpdateDownloadAsync(download);
        DownloadHistory actual = Assert.IsType<DownloadHistory>(await _service.GetDownloadByIdAsync(download.Id));

        Assert.Equal("Failed", actual.Status);
        Assert.Equal("Failure", actual.ErrorMessage);
        Assert.NotNull(actual.FinishedAt);
    }

    [Fact]
    public async Task QueriesOrderAndFilterDownloads()
    {
        await _service.AddDownloadAsync(CreateDownload("Failed", DateTime.UtcNow.AddMinutes(-2)));
        await _service.AddDownloadAsync(CreateDownload("Downloaded", DateTime.UtcNow.AddMinutes(-1)));
        await _service.AddDownloadAsync(CreateDownload("Failed", DateTime.UtcNow));

        List<DownloadHistory> all = await _service.GetAllDownloadsAsync();
        List<DownloadHistory> failed = await _service.GetDownloadsByStatusAsync("Failed");

        Assert.Equal(3, all.Count);
        Assert.True(all[0].CreatedAt >= all[1].CreatedAt);
        Assert.Equal(2, failed.Count);
        Assert.All(failed, item => Assert.Equal("Failed", item.Status));
    }

    [Fact]
    public async Task DeleteAndClearRemoveRecords()
    {
        int firstId = await _service.AddDownloadAsync(CreateDownload("Downloaded", DateTime.UtcNow));
        await _service.AddDownloadAsync(CreateDownload("Failed", DateTime.UtcNow));

        await _service.DeleteDownloadAsync(firstId);
        Assert.Null(await _service.GetDownloadByIdAsync(firstId));
        Assert.Single(await _service.GetAllDownloadsAsync());

        await _service.ClearDownloadsAsync();
        Assert.Empty(await _service.GetAllDownloadsAsync());
    }

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
        return Task.CompletedTask;
    }

    private static DownloadHistory CreateDownload(string status, DateTime createdAt) => new()
    {
        SourceUrl = Guid.NewGuid().ToString("N"),
        Status = status,
        CreatedAt = createdAt
    };
}
