using VideoHarvester.Localization;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using VideoHarvester.Models;

namespace VideoHarvester.Services;

internal static class CdaDownloader
{
    public static async Task Download(Video video, CancellationToken cancellationToken = default)
    {
        video.Status = "Preparing";
        video.ErrorMessage = null;

        try
        {
            bool python = await CommandExists("python", "--version");
            bool ffmpeg = await CommandExists("ffmpeg", "-version");

            if (!python || !ffmpeg)
            {
                video.Status = "Missing dependency";
                video.ErrorMessage = LocalizationManager.Current.Format("Error_MissingDependencies",
                    string.Join(", ", new[] { !python ? "Python" : null, !ffmpeg ? "FFmpeg" : null }.Where(tool => tool is not null)));
                return;
            }

            string outputFolder = Path.GetDirectoryName(video.FilePath)!;
            Directory.CreateDirectory(outputFolder);

            // First, extract metadata
            await ExtractMetadata(video);

            // Check cancellation before starting download
            cancellationToken.ThrowIfCancellationRequested();

            video.Status = "Downloading";

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                WorkingDirectory = outputFolder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string outputFileName = video.UseOrderNumeration ? $"{video.Order}_{video.FileId}" : video.FileId;
            string cdaUrl = $"https://www.cda.pl/video/{video.VideoId}";

            // Build arguments list
            var argumentsList = new List<string>
            {
                "-m", "yt_dlp", cdaUrl
            };

            // Add format selection only if specific quality is requested
            int? maxHeight = VideoQualitySelector.GetMaximumHeight(video.PreferredQuality);
            if (maxHeight.HasValue)
            {
                argumentsList.Add("-f");
                argumentsList.Add($"best[height<={maxHeight}]");
            }
            // If "Best" or no quality specified, don't add -f to let yt-dlp choose automatically

            argumentsList.AddRange(new[]
            {
                "--merge-output-format", "mp4",
                "-o", $"{outputFileName}.%(ext)s",
                "--windows-filenames", "--progress", "--newline"
            });

            foreach (string argument in argumentsList)
                startInfo.ArgumentList.Add(argument);

            using var process = new Process { StartInfo = startInfo };
            var errorOutput = new StringBuilder();

            process.OutputDataReceived += (_, e) => UpdateProgress(video, e.Data);
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    errorOutput.AppendLine(e.Data);
                    UpdateProgress(video, e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait with cancellation support
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && File.Exists(video.FilePath))
            {
                video.Progress = 100;
                video.Status = "Downloaded";

                // Small delay to ensure file handle is fully released
                await Task.Delay(200, cancellationToken);

                // Extract thumbnail after successful download
                await ExtractThumbnail(video);
            }
            else
            {
                video.Status = "Failed";
                string errorText = errorOutput.ToString();
                video.ErrorMessage = string.IsNullOrWhiteSpace(errorText)
                    ? LocalizationManager.Current["Error_DownloadWithoutMessage"]
                    : errorText;
            }
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

    private static async Task ExtractMetadata(Video video)
    {
        try
        {
            string cdaUrl = $"https://www.cda.pl/video/{video.VideoId}";

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string[] arguments =
            [
                "-m", "yt_dlp", cdaUrl,
                "--dump-json",
                "--no-playlist"
            ];

            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                using JsonDocument doc = JsonDocument.Parse(output);
                var root = doc.RootElement;

                if (root.TryGetProperty("title", out var title))
                {
                    video.Title = title.GetString();
                }

                if (root.TryGetProperty("format", out var format))
                {
                    video.Quality = format.GetString();
                }
                else if (root.TryGetProperty("height", out var height))
                {
                    video.Quality = $"{height.GetInt32()}p";
                }
            }
        }
        catch
        {
            // Metadata extraction is optional, continue with download
        }
    }

    private static void UpdateProgress(Video video, string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return;

        // Parse download progress from yt-dlp output
        // Example: [download]  45.8% of 123.45MiB at 1.23MiB/s ETA 00:45
        var match = Regex.Match(data, @"\[download\]\s+(\d+\.?\d*)%");
        if (match.Success && double.TryParse(match.Groups[1].Value, out double progress))
        {
            video.Progress = (int)progress;
        }
    }

    private static async Task<bool> CommandExists(string command, string args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task ExtractThumbnail(Video video)
    {
        try
        {
            // Extract a frame from the video to stdout (pipe)
            var errorOutput = new StringBuilder();
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // ffmpeg -ss 2 -i input.mp4 -vframes 1 -f image2pipe -vcodec mjpeg pipe:1
            string[] arguments =
            [
                "-ss", "2",                          // Seek to 2 seconds
                "-i", video.FilePath,                // Input video file
                "-vframes", "1",                     // Extract 1 frame
                "-vf", "scale=320:-1",               // Scale to 320px width, maintain aspect ratio
                "-f", "image2pipe",                  // Output to pipe
                "-vcodec", "mjpeg",                  // JPEG codec
                "pipe:1"                             // Output to stdout
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

            // Read the image data from stdout
            using var memoryStream = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(memoryStream);
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && memoryStream.Length > 0)
            {
                memoryStream.Position = 0;

                // Create BitmapImage from memory stream
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();
                bitmap.Freeze(); // Freeze for performance and cross-thread access

                video.ThumbnailImage = bitmap;
            }
        }
        catch
        {
            // Thumbnail extraction is optional, don't fail the download
        }
    }
}
