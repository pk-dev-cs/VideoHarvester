using VideoHarvester.Localization;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using VideoHarvester.Models;

namespace VideoHarvester.Services;

internal static class YouTubeDownloader
{
    public static async Task Download(Video video, CancellationToken cancellationToken = default)
    {
        video.Status = "Preparing";
        video.ErrorMessage = null;
        var errorOutput = new StringBuilder();

        string? ytDlpPath = ToolchainLocator.FindYtDlp();
        string? nodePath = ToolchainLocator.FindNode();
        string? ffmpegPath = ToolchainLocator.FindFFmpeg();

        if (ytDlpPath is null || nodePath is null || ffmpegPath is null)
        {
            video.Status = "Missing dependency";
            video.ErrorMessage = LocalizationManager.Current.Format("Error_MissingDependencies",
                string.Join(", ", new[] { ytDlpPath is null ? "yt-dlp" : null, nodePath is null ? "Node.js" : null, ffmpegPath is null ? "FFmpeg" : null }.Where(tool => tool is not null)));
            return;
        }

        string outputFolder = Path.GetDirectoryName(video.FilePath)!;
        Directory.CreateDirectory(outputFolder);

        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        string outputFileName = video.UseOrderNumeration ? $"{video.Order}_{video.FileId}" : video.FileId;
        string fullOutputPath = Path.Combine(outputFolder, $"{outputFileName}.%(ext)s");
        string formatSelector = VideoQualitySelector.GetYouTubeFormat(video.PreferredQuality);
        string? browser = DefaultBrowserCookies.GetBrowserName();

        string[] arguments =
        [
            video.VideoId,
            "--js-runtimes", $"node:{nodePath}",
            "--ffmpeg-location", ffmpegPath,
            "-f", formatSelector,
            "--check-formats",
            "--merge-output-format", "mkv",
            "--remux-video", "mkv",
            "--no-playlist",
            "-o", fullOutputPath,
            "--windows-filenames", "--progress", "--newline",
            "--no-overwrites"
        ];

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);
        DefaultBrowserCookies.AddTo(startInfo, browser);

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => UpdateProgress(video, e.Data);
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                errorOutput.AppendLine(e.Data);
                UpdateProgress(video, e.Data);
            }
        };

        try
        {
            // First, extract metadata
            await ExtractMetadata(video, ytDlpPath, nodePath, ffmpegPath, formatSelector, browser);

            // Check cancellation before starting download
            cancellationToken.ThrowIfCancellationRequested();

            video.Status = "Downloading";
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait with cancellation support
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && File.Exists(video.FilePath))
            {
                video.Progress = 100;
                video.Status = "Downloaded";

                // Extract thumbnail after successful download
                await ExtractThumbnail(video, ffmpegPath);
            }
            else
            {
                video.Status = "Failed";

                // Build detailed error message
                var errorDetails = new StringBuilder();
                errorDetails.AppendLine(LocalizationManager.Current.Format("Error_ExitCode", process.ExitCode));
                errorDetails.AppendLine(LocalizationManager.Current.Format("Error_ExpectedFile", video.FilePath));
                errorDetails.AppendLine(LocalizationManager.Current.Format("Error_FileExists", LocalizationManager.Current[File.Exists(video.FilePath) ? "Common_Yes" : "Common_No"]));

                if (errorOutput.Length > 0)
                {
                    errorDetails.AppendLine(LocalizationManager.Current["Error_YtDlpOutput"]);
                    // Get last 500 characters of error output to avoid too much data
                    string errorText = errorOutput.ToString();
                    errorDetails.Append(errorText.Length > 500 ? "..." + errorText.Substring(errorText.Length - 500) : errorText);
                }

                video.ErrorMessage = errorDetails.ToString();
            }
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch { /* Ignore errors when killing process */ }

            video.Status = "Cancelled";
            throw;
        }
        catch (Exception ex)
        {
            video.Status = "Failed";
            video.ErrorMessage = LocalizationManager.Current.Format("Error_Exception", ex.Message, ex.StackTrace);
        }
    }

    private static void UpdateProgress(Video video, string? line)
    {
        Match match = Regex.Match(line ?? string.Empty, @"\[download\]\s+(\d+(?:\.\d+)?)%");
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out double progress))
            video.Progress = (int)Math.Round(progress);
    }

    private static async Task ExtractMetadata(Video video, string ytDlpPath, string nodePath, string ffmpegPath, string formatSelector, string? browser)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string[] arguments =
            [
                video.VideoId,
                "--js-runtimes", $"node:{nodePath}",
                "--ffmpeg-location", ffmpegPath,
                "-f", formatSelector,
                "--dump-json",
                "--no-playlist"
            ];

            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);
            DefaultBrowserCookies.AddTo(startInfo, browser);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                // Parse JSON to extract title and format info
                var json = System.Text.Json.JsonDocument.Parse(output);
                var root = json.RootElement;

                if (root.TryGetProperty("title", out var titleElement))
                {
                    video.Title = titleElement.GetString();
                }

                if (root.TryGetProperty("height", out var heightElement) && heightElement.TryGetInt32(out int height))
                {
                    video.Quality = $"{height}p";
                }
                else if (root.TryGetProperty("format", out var formatElement))
                {
                    video.Quality = formatElement.GetString();
                }
                else if (root.TryGetProperty("format_id", out var formatIdElement))
                {
                    video.Quality = formatIdElement.GetString();
                }
            }
        }
        catch
        {
            // If metadata extraction fails, continue without it
        }
    }

    private static async Task ExtractThumbnail(Video video, string ffmpegPath)
    {
        try
        {
            // Extract a frame from the video to stdout (pipe)
            var errorOutput = new StringBuilder();
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // ffmpeg -ss 2 -i input.mkv -vframes 1 -f image2pipe -vcodec mjpeg pipe:1
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
            else
            {
                // Add error info to help debug
                var debugInfo = new StringBuilder();
                if (video.ErrorMessage != null)
                    debugInfo.AppendLine(video.ErrorMessage);
                debugInfo.AppendLine(LocalizationManager.Current["Error_ThumbnailFailed"]);
                debugInfo.AppendLine(LocalizationManager.Current.Format("Error_ExitCode", process.ExitCode));
                debugInfo.AppendLine(LocalizationManager.Current.Format("Error_StreamLength", memoryStream.Length));
                debugInfo.AppendLine(LocalizationManager.Current.Format("Error_VideoPath", video.FilePath));
                debugInfo.AppendLine(LocalizationManager.Current.Format("Error_FileExists", LocalizationManager.Current[File.Exists(video.FilePath) ? "Common_Yes" : "Common_No"]));
                if (errorOutput.Length > 0)
                {
                    debugInfo.AppendLine(LocalizationManager.Current["Error_FFmpegOutput"]);
                    string errorText = errorOutput.ToString();
                    debugInfo.Append(errorText.Length > 500 ? "..." + errorText.Substring(errorText.Length - 500) : errorText);
                }
                video.ErrorMessage = debugInfo.ToString();
            }
        }
        catch (Exception ex)
        {
            // Add exception info for debugging
            var debugInfo = new StringBuilder();
            if (video.ErrorMessage != null)
                debugInfo.AppendLine(video.ErrorMessage);
            debugInfo.AppendLine(LocalizationManager.Current.Format("Error_ThumbnailException", ex.Message));
            video.ErrorMessage = debugInfo.ToString();
        }
    }
}
