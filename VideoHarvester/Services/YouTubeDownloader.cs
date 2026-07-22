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
    public static async Task Download(Video video)
    {
        video.Status = "Preparing";
        video.ErrorMessage = null;
        var errorOutput = new StringBuilder();

        string? nodePath = FindNode();
        bool python = await CommandExists("python", "--version");
        bool ffmpeg = await CommandExists("ffmpeg", "-version");

        if (nodePath is null || !python || !ffmpeg)
        {
            video.Status = "Missing dependency";
            video.ErrorMessage = $"Missing: {(nodePath is null ? "Node.js " : "")}{(!python ? "Python " : "")}{(!ffmpeg ? "FFmpeg" : "")}";
            return;
        }

        string outputFolder = Path.GetDirectoryName(video.FilePath)!;
        Directory.CreateDirectory(outputFolder);

        var startInfo = new ProcessStartInfo
        {
            FileName = "python",
            WorkingDirectory = outputFolder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        string[] arguments =
        [
            "-m", "yt_dlp", video.VideoId,
            "--js-runtimes", $"node:{nodePath}",
            "--cookies-from-browser", "firefox",
            "-f", "bestvideo+bestaudio",
            "--check-formats",
            "--merge-output-format", "mkv",
            "--remux-video", "mkv",
            "--no-playlist",
            "-o", $"{video.Order}.%(ext)s",
            "--windows-filenames", "--progress", "--newline"
        ];

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

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
            await ExtractMetadata(video, nodePath);

            video.Status = "Downloading";
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(video.FilePath))
            {
                video.Progress = 100;
                video.Status = "Downloaded";

                // Extract thumbnail after successful download
                await ExtractThumbnail(video);
            }
            else
            {
                video.Status = "Failed";

                // Build detailed error message
                var errorDetails = new StringBuilder();
                errorDetails.AppendLine($"Exit Code: {process.ExitCode}");
                errorDetails.AppendLine($"Expected File: {video.FilePath}");
                errorDetails.AppendLine($"File Exists: {File.Exists(video.FilePath)}");

                if (errorOutput.Length > 0)
                {
                    errorDetails.AppendLine("yt_dlp output:");
                    // Get last 500 characters of error output to avoid too much data
                    string errorText = errorOutput.ToString();
                    errorDetails.Append(errorText.Length > 500 ? "..." + errorText.Substring(errorText.Length - 500) : errorText);
                }

                video.ErrorMessage = errorDetails.ToString();
            }
        }
        catch (Exception ex)
        {
            video.Status = "Failed";
            video.ErrorMessage = $"Exception: {ex.Message}\n{ex.StackTrace}";
        }
    }

    private static void UpdateProgress(Video video, string? line)
    {
        Match match = Regex.Match(line ?? string.Empty, @"\[download\]\s+(\d+(?:\.\d+)?)%");
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out double progress))
            video.Progress = (int)Math.Round(progress);
    }

    private static string? FindNode()
    {
        string[] commonPaths = [@"C:\Program Files\nodejs\node.exe", @"C:\Program Files (x86)\nodejs\node.exe"];
        foreach (string path in commonPaths)
            if (File.Exists(path)) return path;

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable)) return null;

        foreach (string folder in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string nodePath = Path.Combine(folder.Trim(), "node.exe");
                if (File.Exists(nodePath)) return nodePath;
            }
            catch (Exception) { }
        }

        return null;
    }

    private static async Task<bool> CommandExists(string command, string testArgument)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(testArgument);
            using Process? process = Process.Start(startInfo);
            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception) { return false; }
    }

    private static async Task ExtractMetadata(Video video, string nodePath)
    {
        try
        {
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
                "-m", "yt_dlp", video.VideoId,
                "--js-runtimes", $"node:{nodePath}",
                "--cookies-from-browser", "firefox",
                "--dump-json",
                "--no-playlist"
            ];

            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

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

                if (root.TryGetProperty("format", out var formatElement))
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
            // Add exception info for debugging
            var debugInfo = new StringBuilder();
            if (video.ErrorMessage != null)
                debugInfo.AppendLine(video.ErrorMessage);
            debugInfo.AppendLine($"Thumbnail exception: {ex.Message}");
            video.ErrorMessage = debugInfo.ToString();
        }
    }
}
