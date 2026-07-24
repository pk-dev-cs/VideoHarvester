using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoHarvester.Models;

namespace VideoHarvester.Services;

internal static class YouTubeWavDownloader
{
    public static async Task Download(Video video, CancellationToken cancellationToken = default)
    {
        video.Status = "Preparing";

        string? nodePath = FindNode();
        if (nodePath is null || !await CommandExists("python", "--version") || !await CommandExists("ffmpeg", "-version"))
        {
            video.Status = "Missing dependency";
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

        string outputFileName = video.UseOrderNumeration ? $"{video.Order}_{video.FileId}" : video.FileId;

        string[] arguments =
        [
            "-m", "yt_dlp", video.VideoId,
            "--js-runtimes", $"node:{nodePath}",
            "--remote-components", "ejs:github",
            "--cookies-from-browser", "firefox",
            "--extractor-args", "youtube:player_client=tv",
            "--extract-audio",
            "--audio-format", "wav",
            "--no-playlist",
            "-o", $"{outputFileName}.%(ext)s",
            "--windows-filenames",
            "--newline", "--progress", "--verbose"
        ];

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => UpdateProgress(video, e.Data);
        process.ErrorDataReceived += (_, e) => UpdateProgress(video, e.Data);

        try
        {
            // First, extract metadata
            await ExtractMetadata(video, nodePath);

            // Check cancellation before starting download
            cancellationToken.ThrowIfCancellationRequested();

            video.Status = "Downloading";
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && File.Exists(video.FilePath))
            {
                video.Progress = 100;
                video.Status = "Downloaded";
            }
            else
            {
                video.Status = "Failed";
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
        catch (Exception)
        {
            video.Status = "Failed";
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
                var json = JsonDocument.Parse(output);
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
}
