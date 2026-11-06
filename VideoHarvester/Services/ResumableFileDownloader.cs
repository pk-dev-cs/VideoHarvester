using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace VideoHarvester.Services;

internal static class ResumableFileDownloader
{
    public static async Task DownloadAsync(HttpClient client, string url, string destination, Action<int> reportProgress, CancellationToken cancellationToken)
    {
        string partialPath = destination + ".part";
        string statePath = partialPath + ".json";

        await using (var file = new FileStream(partialPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 8192, true))
        {
            long offset = file.Length;
            DownloadState? state = null;
            if (offset > 0)
            {
                if (!File.Exists(statePath))
                    throw new IOException("Cannot safely resume: download information is missing. The partial file was preserved.");

                state = JsonSerializer.Deserialize<DownloadState>(await File.ReadAllTextAsync(statePath, cancellationToken));
                if (state is null || state.Url != url || (state.Length.HasValue && offset > state.Length.Value))
                    throw new IOException("Cannot safely resume: the source or partial file has changed. The partial file was preserved.");
            }

            if (state?.Length != offset || offset == 0)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.AcceptEncoding.ParseAdd("identity");
                if (offset > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(offset, null);
                    if (state!.ETag is not null)
                        request.Headers.IfRange = new RangeConditionHeaderValue(EntityTagHeaderValue.Parse(state.ETag));
                    else if (state.LastModified.HasValue)
                        request.Headers.IfRange = new RangeConditionHeaderValue(state.LastModified.Value);
                    else
                        throw new IOException("Cannot safely resume: the server supplied no file validator. The partial file was preserved.");
                }

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                long? totalBytes;
                if (offset > 0)
                {
                    var range = response.Content.Headers.ContentRange;
                    if (response.StatusCode != HttpStatusCode.PartialContent || range?.Unit != "bytes" ||
                        range.From != offset || !range.To.HasValue || !range.Length.HasValue ||
                        range.To.Value != range.Length.Value - 1 ||
                        (state!.Length.HasValue && range.Length != state.Length) ||
                        (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength != range.Length - offset) ||
                        (state.ETag is not null && response.Headers.ETag is not null && response.Headers.ETag.ToString() != state.ETag) ||
                        (state.ETag is null && response.Content.Headers.LastModified.HasValue && response.Content.Headers.LastModified != state.LastModified))
                    {
                        throw new IOException("The server cannot resume this file safely. The partial file was preserved; restarting requires removing its .part and .part.json files.");
                    }

                    totalBytes = range.Length;
                    if (state.Length != totalBytes)
                    {
                        state = state with { Length = totalBytes };
                        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state), cancellationToken);
                    }
                }
                else
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new IOException("The server did not return a complete download response.");

                    totalBytes = response.Content.Headers.ContentLength;
                    state = new DownloadState(url, response.Headers.ETag is { IsWeak: false } tag ? tag.ToString() : null,
                        response.Content.Headers.LastModified, totalBytes);
                    await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state), cancellationToken);
                }

                file.Position = offset;
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                byte[] buffer = new byte[8192];
                try
                {
                    int bytesRead;
                    while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        if (totalBytes.HasValue && file.Position + bytesRead > totalBytes.Value)
                            throw new IOException("The server returned more data than expected. The partial file was preserved.");

                        await file.WriteAsync(buffer.AsMemory(0, bytesRead), CancellationToken.None);
                        if (totalBytes > 0)
                            reportProgress((int)Math.Clamp(file.Position * 100.0 / totalBytes.Value, 0, 99));
                    }
                }
                finally
                {
                    await file.FlushAsync(CancellationToken.None);
                }

                if (totalBytes.HasValue && file.Length != totalBytes.Value)
                    throw new IOException("The download ended before the complete file arrived. The partial file was preserved for resuming.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        File.Move(partialPath, destination, true);
        File.Delete(statePath);
        reportProgress(100);
    }

    private sealed record DownloadState(string Url, string? ETag, DateTimeOffset? LastModified, long? Length);
}
