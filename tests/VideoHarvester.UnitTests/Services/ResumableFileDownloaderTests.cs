using System.Net;
using System.Net.Http.Headers;
using System.Text;
using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.Services;

public class ResumableFileDownloaderTests : IDisposable
{
    private const string Url = "https://example.test/video.mp4";
    private static readonly byte[] Data = Encoding.UTF8.GetBytes("0123456789");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "VideoHarvester-resume-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FreshDownloadCreatesOnlyFinalFile()
    {
        string path = GetPath("fresh");
        using var client = new HttpClient(new StubHandler(_ => Response(HttpStatusCode.OK, Data)));

        await ResumableFileDownloader.DownloadAsync(client, Url, path, _ => { }, CancellationToken.None);

        Assert.Equal(Data, await File.ReadAllBytesAsync(path));
        Assert.False(File.Exists(path + ".part"));
        Assert.False(File.Exists(path + ".part.json"));
    }

    [Fact]
    public async Task InterruptedDownloadResumesWithRangeAndValidator()
    {
        string path = GetPath("resume");
        byte[] partial = await CreateInterruptedDownload(path, addValidator: true);
        using var client = new HttpClient(new StubHandler(request =>
        {
            Assert.Equal(partial.Length, request.Headers.Range?.Ranges.Single().From);
            Assert.Equal("\"version-1\"", request.Headers.IfRange?.EntityTag?.Tag);
            var response = Response(HttpStatusCode.PartialContent, Data[partial.Length..]);
            response.Headers.ETag = new EntityTagHeaderValue("\"version-1\"");
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(partial.Length, Data.Length - 1, Data.Length);
            return response;
        }));

        var progress = new List<int>();
        await ResumableFileDownloader.DownloadAsync(client, Url, path, progress.Add, CancellationToken.None);

        Assert.Equal(Data, await File.ReadAllBytesAsync(path));
        Assert.Equal(100, progress.Last());
        Assert.False(File.Exists(path + ".part.json"));
    }

    [Fact]
    public async Task LastModifiedCanValidateResume()
    {
        string path = GetPath("last-modified");
        using var cancellation = new CancellationTokenSource();
        using (var firstClient = new HttpClient(new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new InterruptedStream(Data, cancellation, 3)) };
            response.Content.Headers.ContentLength = Data.Length;
            response.Content.Headers.LastModified = DateTimeOffset.UnixEpoch;
            return response;
        })))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ResumableFileDownloader.DownloadAsync(firstClient, Url, path, _ => { }, cancellation.Token));
        }

        int partialLength = (await File.ReadAllBytesAsync(path + ".part")).Length;
        using var resumedClient = new HttpClient(new StubHandler(request =>
        {
            Assert.Equal(DateTimeOffset.UnixEpoch, request.Headers.IfRange?.Date);
            var response = Response(HttpStatusCode.PartialContent, Data[partialLength..]);
            response.Content.Headers.LastModified = DateTimeOffset.UnixEpoch;
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(partialLength, Data.Length - 1, Data.Length);
            return response;
        }));

        await ResumableFileDownloader.DownloadAsync(resumedClient, Url, path, _ => { }, CancellationToken.None);
        Assert.Equal(Data, await File.ReadAllBytesAsync(path));
    }

    [Theory]
    [InlineData("ignored-range")]
    [InlineData("wrong-offset")]
    [InlineData("changed-etag")]
    public async Task UnsafeResumeIsRejectedWithoutChangingPartialFile(string scenario)
    {
        string path = GetPath(scenario);
        byte[] partial = await CreateInterruptedDownload(path, addValidator: true);
        using var client = new HttpClient(new StubHandler(_ =>
        {
            var response = Response(scenario == "ignored-range" ? HttpStatusCode.OK : HttpStatusCode.PartialContent, Data[partial.Length..]);
            response.Headers.ETag = new EntityTagHeaderValue(scenario == "changed-etag" ? "\"version-2\"" : "\"version-1\"");
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(scenario == "wrong-offset" ? 0 : partial.Length, Data.Length - 1, Data.Length);
            return response;
        }));

        await Assert.ThrowsAsync<IOException>(() => ResumableFileDownloader.DownloadAsync(client, Url, path, _ => { }, CancellationToken.None));

        Assert.Equal(partial, await File.ReadAllBytesAsync(path + ".part"));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ResumeWithoutValidatorIsRejectedBeforeRequest()
    {
        string path = GetPath("no-validator");
        await CreateInterruptedDownload(path, addValidator: false);
        int requests = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            requests++;
            return Response(HttpStatusCode.PartialContent, []);
        }));

        await Assert.ThrowsAsync<IOException>(() => ResumableFileDownloader.DownloadAsync(client, Url, path, _ => { }, CancellationToken.None));
        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task CompletePartialFileIsFinalizedWithoutRequest()
    {
        string path = GetPath("complete-partial");
        await CreateInterruptedDownload(path, addValidator: true, chunkSize: Data.Length);
        int requests = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            requests++;
            return Response(HttpStatusCode.PartialContent, []);
        }));

        await ResumableFileDownloader.DownloadAsync(client, Url, path, _ => { }, CancellationToken.None);

        Assert.Equal(0, requests);
        Assert.Equal(Data, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task TruncatedResponsePreservesPartialData()
    {
        string path = GetPath("truncated");
        using var client = new HttpClient(new StubHandler(_ =>
        {
            var response = Response(HttpStatusCode.OK, Data[..3]);
            response.Content.Headers.ContentLength = Data.Length;
            response.Headers.ETag = new EntityTagHeaderValue("\"version-1\"");
            return response;
        }));

        await Assert.ThrowsAsync<IOException>(() => ResumableFileDownloader.DownloadAsync(client, Url, path, _ => { }, CancellationToken.None));

        Assert.Equal(Data[..3], await File.ReadAllBytesAsync(path + ".part"));
        Assert.True(File.Exists(path + ".part.json"));
    }

    private async Task<byte[]> CreateInterruptedDownload(string path, bool addValidator, int chunkSize = 3)
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new HttpClient(new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new InterruptedStream(Data, cancellation, chunkSize)) };
            response.Content.Headers.ContentLength = Data.Length;
            if (addValidator)
                response.Headers.ETag = new EntityTagHeaderValue("\"version-1\"");
            return response;
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ResumableFileDownloader.DownloadAsync(client, Url, path, _ => { }, cancellation.Token));
        return await File.ReadAllBytesAsync(path + ".part");
    }

    private string GetPath(string name)
    {
        Directory.CreateDirectory(_folder);
        return Path.Combine(_folder, name + ".mp4");
    }

    private static HttpResponseMessage Response(HttpStatusCode status, byte[] content) => new(status) { Content = new ByteArrayContent(content) };

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }

    private sealed class InterruptedStream(byte[] data, CancellationTokenSource cancellation, int chunkSize) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = Read(buffer.Span[..Math.Min(buffer.Length, chunkSize)]);
            if (count > 0)
                cancellation.Cancel();
            return ValueTask.FromResult(count);
        }
    }
}
