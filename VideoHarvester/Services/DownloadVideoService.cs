using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoHarvester.Models;

namespace VideoHarvester.Services;

public interface IDownloadVideoService
{
    Task DownloadVideo(Video video);
}

public class DownloadVideoService : IDownloadVideoService
{
    public async Task DownloadVideo(Video video)
    {
        string apiUrl = $"http://fast.wistia.net/embed/iframe/{video.VideoId}";

        using HttpClient client = new();
        client.Timeout = TimeSpan.FromMinutes(10);

        try
        {
            video.Status = "Downloading";

            string response = await client.GetStringAsync(apiUrl);
            Match match = Regex.Match(response, "W\\.iframeInit\\((\\{.*?\\}), \\{");
            if (!match.Success)
            {
                video.Status = "Failed";
                return;
            }

            string jsonString = match.Groups[1].Value;
            using JsonDocument jsonDocument = JsonDocument.Parse(jsonString);
            string? videoUrl = jsonDocument.RootElement.GetProperty("assets")[0].GetProperty("url").GetString();

            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                video.Status = "Failed";
                return;
            }

            videoUrl = videoUrl.Replace(".bin", ".mp4");

            string outputFile = video.FilePath;

            using HttpResponseMessage videoResponse = await client.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead);
            using Stream videoStream = await videoResponse.Content.ReadAsStreamAsync();
            using FileStream fileStream = new(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[8192];
            long totalBytes = videoResponse.Content.Headers.ContentLength ?? -1;
            long receivedBytes = 0;
            int bytesRead;

            while ((bytesRead = await videoStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                receivedBytes += bytesRead;

                if (totalBytes > 0)
                    video.Progress = (int)(receivedBytes * 100 / totalBytes);
            }

            video.Progress = 100;
            video.Status = "Downloaded";
        }
        catch
        {
            video.Status = "Failed";
        }
    }
}
