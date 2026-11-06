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
    public static async Task Download(Video video, CancellationToken cancellationToken = default)
    {
        string apiUrl = $"http://fast.wistia.net/embed/iframe/{video.VideoId}";
        using HttpClient client = new() { Timeout = TimeSpan.FromMinutes(10) };

        try
        {
            video.ErrorMessage = null;
            video.Status = "Downloading";
            string response = await client.GetStringAsync(apiUrl, cancellationToken);
            Match match = Regex.Match(response, "W\\.iframeInit\\((\\{.*?\\}), \\{");
            if (!match.Success)
            {
                video.Status = "Failed";
                return;
            }

            using JsonDocument document = JsonDocument.Parse(match.Groups[1].Value);
            var root = document.RootElement;

            // Extract metadata
            if (root.TryGetProperty("name", out var nameElement))
            {
                video.Title = nameElement.GetString();
            }

            if (!root.TryGetProperty("assets", out var assetsElement) || assetsElement.GetArrayLength() == 0)
            {
                video.Status = "Failed";
                video.ErrorMessage = "Wistia did not return any downloadable video assets.";
                return;
            }

            JsonElement? selectedAsset = SelectVideoAsset(assetsElement, video.PreferredQuality);
            if (selectedAsset is null ||
                !selectedAsset.Value.TryGetProperty("url", out var urlElement) ||
                string.IsNullOrWhiteSpace(urlElement.GetString()))
            {
                video.Status = "Failed";
                video.ErrorMessage = $"No Wistia video asset is available for quality {video.PreferredQuality}.";
                return;
            }

            int selectedHeight = GetAssetHeight(selectedAsset.Value);
            video.Quality = selectedAsset.Value.TryGetProperty("display_name", out var displayNameElement)
                ? displayNameElement.GetString()
                : selectedHeight > 0 ? $"{selectedHeight}p" : video.PreferredQuality;

            string videoUrl = urlElement.GetString()!.Replace(".bin", ".mp4");
            await ResumableFileDownloader.DownloadAsync(client, videoUrl, video.FilePath,
                progress => video.Progress = progress, cancellationToken);

            video.Progress = 100;
            video.Status = "Downloaded";

            // Extract thumbnail after successful download
            await ExtractThumbnail(video);
        }
        catch (OperationCanceledException)
        {
            video.Status = "Cancelled";
            throw;
        }
        catch (Exception ex)
        {
            video.Status = "Failed";
            video.ErrorMessage = ex.Message;
        }
    }

    private static JsonElement? SelectVideoAsset(JsonElement assets, string preferredQuality)
    {
        int? maximumHeight = VideoQualitySelector.GetMaximumHeight(preferredQuality);
        JsonElement? bestMatch = null;
        int bestMatchHeight = -1;
        JsonElement? lowestQuality = null;
        int lowestHeight = int.MaxValue;

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("url", out var urlElement) ||
                string.IsNullOrWhiteSpace(urlElement.GetString()) ||
                IsNonVideoAsset(asset))
            {
                continue;
            }

            int height = GetAssetHeight(asset);
            if (height <= 0)
                continue;

            if (height < lowestHeight)
            {
                lowestHeight = height;
                lowestQuality = asset;
            }

            if ((maximumHeight is null || height <= maximumHeight) && height > bestMatchHeight)
            {
                bestMatchHeight = height;
                bestMatch = asset;
            }
        }

        return bestMatch ?? lowestQuality;
    }

    private static bool IsNonVideoAsset(JsonElement asset)
    {
        if (!asset.TryGetProperty("type", out var typeElement))
            return false;

        string type = typeElement.GetString() ?? string.Empty;
        return type.Contains("image", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("still", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("storyboard", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("audio", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetAssetHeight(JsonElement asset)
    {
        if (asset.TryGetProperty("height", out var heightElement) &&
            heightElement.TryGetInt32(out int height))
        {
            return height;
        }

        if (asset.TryGetProperty("display_name", out var displayNameElement))
        {
            Match match = Regex.Match(displayNameElement.GetString() ?? string.Empty, @"(?<height>\d{3,4})p");
            if (match.Success && int.TryParse(match.Groups["height"].Value, out height))
                return height;
        }

        return 0;
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
