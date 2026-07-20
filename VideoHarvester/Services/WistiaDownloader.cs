using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using VideoHarvester.Models;

namespace VideoHarvester.Services;

internal static class WistiaDownloader
{
    public static async Task Download(Video video)
    {
        string apiUrl = $"http://fast.wistia.net/embed/iframe/{video.VideoId}";
        using HttpClient client = new() { Timeout = TimeSpan.FromMinutes(10) };

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

            using JsonDocument document = JsonDocument.Parse(match.Groups[1].Value);
            string? videoUrl = document.RootElement.GetProperty("assets")[0].GetProperty("url").GetString();
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                video.Status = "Failed";
                return;
            }

            videoUrl = videoUrl.Replace(".bin", ".mp4");
            using HttpResponseMessage videoResponse = await client.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead);
            videoResponse.EnsureSuccessStatusCode();
            await using Stream videoStream = await videoResponse.Content.ReadAsStreamAsync();
            await using FileStream fileStream = new(video.FilePath, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[8192];
            long totalBytes = videoResponse.Content.Headers.ContentLength ?? -1;
            long receivedBytes = 0;
            int bytesRead;

            while ((bytesRead = await videoStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                receivedBytes += bytesRead;
                if (totalBytes > 0)
                    video.Progress = (int)(receivedBytes * 100 / totalBytes);
            }

            video.Progress = 100;
            video.Status = "Downloaded";

            // Extract thumbnail after successful download
            await ExtractThumbnail(video);
        }
        catch (Exception)
        {
            video.Status = "Failed";
        }
    }

    private static async Task ExtractThumbnail(Video video)
    {
        try
        {
            var errorOutput = new StringBuilder();
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string[] arguments =
            [
                "-ss", "2",
                "-i", video.FilePath,
                "-vframes", "1",
                "-vf", "scale=320:-1",
                "-f", "image2pipe",
                "-vcodec", "mjpeg",
                "pipe:1"
            ];

            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = new Process { StartInfo = startInfo };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    errorOutput.AppendLine(e.Data);
            };

            process.Start();
            process.BeginErrorReadLine();

            using var memoryStream = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(memoryStream);
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && memoryStream.Length > 0)
            {
                memoryStream.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();
                bitmap.Freeze();

                video.ThumbnailImage = bitmap;
            }
            else
            {
                var debugInfo = new StringBuilder();
                if (video.ErrorMessage != null)
                    debugInfo.AppendLine(video.ErrorMessage);
                debugInfo.AppendLine($"Thumbnail extraction failed:");
                debugInfo.AppendLine($"Exit Code: {process.ExitCode}");
                debugInfo.AppendLine($"Stream Length: {memoryStream.Length} bytes");
                debugInfo.AppendLine($"Video Path: {video.FilePath}");
                debugInfo.AppendLine($"Video Exists: {File.Exists(video.FilePath)}");
                if (errorOutput.Length > 0)
                {
                    debugInfo.AppendLine("FFmpeg output:");
                    string errorText = errorOutput.ToString();
                    debugInfo.Append(errorText.Length > 500 ? "..." + errorText.Substring(errorText.Length - 500) : errorText);
                }
                video.ErrorMessage = debugInfo.ToString();
            }
        }
        catch (Exception ex)
        {
            var debugInfo = new StringBuilder();
            if (video.ErrorMessage != null)
                debugInfo.AppendLine(video.ErrorMessage);
            debugInfo.AppendLine($"Thumbnail exception: {ex.Message}");
            video.ErrorMessage = debugInfo.ToString();
        }
    }
}
