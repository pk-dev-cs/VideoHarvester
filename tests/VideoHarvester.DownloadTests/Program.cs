using System.Net;
using System.Net.Http.Headers;
using System.Text;
using VideoHarvester.Services;

string toolsDirectory = Path.Combine(AppContext.BaseDirectory, "tools");
Directory.CreateDirectory(toolsDirectory);
try
{
    foreach (string fileName in new[] { "yt-dlp.exe", "node.exe", "ffmpeg.exe", "ffprobe.exe" })
        await File.WriteAllBytesAsync(Path.Combine(toolsDirectory, fileName), []);

    Check(ToolchainLocator.FindYtDlp() == Path.Combine(toolsDirectory, "yt-dlp.exe"), "Bundled yt-dlp must take precedence.");
    Check(ToolchainLocator.FindNode() == Path.Combine(toolsDirectory, "node.exe"), "Bundled Node.js must take precedence.");
    Check(ToolchainLocator.FindFFmpeg() == Path.Combine(toolsDirectory, "ffmpeg.exe"), "Bundled FFmpeg must take precedence.");
    Check(ToolchainLocator.FindFFprobe() == Path.Combine(toolsDirectory, "ffprobe.exe"), "Bundled FFprobe must take precedence.");
    Console.WriteLine("PASS bundled toolchain");
}
finally
{
    Directory.Delete(toolsDirectory, true);
}

const string url = "https://example.test/video.mp4";
byte[] data = Encoding.UTF8.GetBytes("0123456789");
string folder = Path.Combine(Path.GetTempPath(), "VideoHarvester-download-tests-" + Guid.NewGuid());
Directory.CreateDirectory(folder);

try
{
    string freshPath = Path.Combine(folder, "fresh.mp4");
    using (var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) })))
    {
        await ResumableFileDownloader.DownloadAsync(client, url, freshPath, _ => { }, CancellationToken.None);
        Check((await File.ReadAllBytesAsync(freshPath)).SequenceEqual(data), "A fresh download must match the source.");
        Check(!File.Exists(freshPath + ".part") && !File.Exists(freshPath + ".part.json"), "A fresh completed download must remove resume files.");
        Console.WriteLine("PASS fresh");
    }

    foreach (string scenario in new[] { "resume", "last-modified", "ignored-range", "wrong-offset", "changed-etag", "no-validator", "truncated", "complete-partial" })
    {
        string path = Path.Combine(folder, scenario + ".mp4");
        using var cancellation = new CancellationTokenSource();
        using var firstClient = new HttpClient(new StubHandler(_ =>
        {
            Stream stream = scenario == "truncated" ? new MemoryStream(data[..3]) : new InterruptedStream(data, cancellation, scenario == "complete-partial" ? data.Length : 3);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
            response.Content.Headers.ContentLength = data.Length;
            if (scenario == "last-modified")
                response.Content.Headers.LastModified = DateTimeOffset.UnixEpoch;
            else if (scenario != "no-validator")
                response.Headers.ETag = new EntityTagHeaderValue("\"version-1\"");
            return response;
        }));

        await ExpectFailure(() => ResumableFileDownloader.DownloadAsync(firstClient, url, path, _ => { }, cancellation.Token));
        byte[] partial = await File.ReadAllBytesAsync(path + ".part");
        Check(partial.SequenceEqual(data[..partial.Length]), "Partial file must contain the correct prefix.");
        Check(!File.Exists(path), "Incomplete download must not appear as the final file.");
        Check(File.Exists(path + ".part.json"), "Resume metadata must be preserved.");

        int requests = 0;
        using var resumedClient = new HttpClient(new StubHandler(request =>
        {
            requests++;
            Check(request.Headers.Range?.Ranges.Single().From == partial.Length, "Range must start at the saved offset.");
            if (scenario == "last-modified")
                Check(request.Headers.IfRange?.Date == DateTimeOffset.UnixEpoch, "If-Range must use the original modification date.");
            else
                Check(request.Headers.IfRange?.EntityTag?.Tag == "\"version-1\"", "If-Range must use the original validator.");
            var response = new HttpResponseMessage(scenario == "ignored-range" ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent(data[partial.Length..])
            };
            if (scenario == "last-modified")
                response.Content.Headers.LastModified = DateTimeOffset.UnixEpoch;
            else
                response.Headers.ETag = new EntityTagHeaderValue(scenario == "changed-etag" ? "\"version-2\"" : "\"version-1\"");
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(scenario == "wrong-offset" ? 0 : partial.Length, data.Length - 1, data.Length);
            return response;
        }));

        if (scenario is "resume" or "last-modified" or "truncated" or "complete-partial")
        {
            var progress = new List<int>();
            await ResumableFileDownloader.DownloadAsync(resumedClient, url, path, progress.Add, CancellationToken.None);
            Check((await File.ReadAllBytesAsync(path)).SequenceEqual(data), "Resumed file must match the original exactly.");
            Check(!File.Exists(path + ".part") && !File.Exists(path + ".part.json"), "Completed downloads must remove resume files.");
            Check(progress.Last() == 100, "Completion must report 100%.");
            if (scenario == "complete-partial")
                Check(requests == 0, "A fully saved partial file should be finalized without another request.");
        }
        else
        {
            await ExpectFailure(() => ResumableFileDownloader.DownloadAsync(resumedClient, url, path, _ => { }, CancellationToken.None));
            Check((await File.ReadAllBytesAsync(path + ".part")).SequenceEqual(partial), "Rejected resume must not alter saved bytes.");
            Check(!File.Exists(path), "Rejected resume must not create the final file.");
            if (scenario == "no-validator")
                Check(requests == 0, "Missing validators must be rejected before requesting more bytes.");
        }

        Console.WriteLine($"PASS {scenario}");
    }
}
finally
{
    Directory.Delete(folder, true);
}

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static async Task ExpectFailure(Func<Task> operation)
{
    try
    {
        await operation();
    }
    catch (Exception ex) when (ex is OperationCanceledException or IOException)
    {
        return;
    }

    throw new InvalidOperationException("Expected a cancelled or rejected download.");
}

sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
}

sealed class InterruptedStream(byte[] data, CancellationTokenSource cancellation, int chunkSize) : MemoryStream(data)
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
