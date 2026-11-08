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

        string? ytDlpPath = ToolchainLocator.FindYtDlp();
        string? nodePath = ToolchainLocator.FindNode();
        string? ffmpegPath = ToolchainLocator.FindFFmpeg();
        if (ytDlpPath is null || nodePath is null || ffmpegPath is null)
        {
            video.Status = "Missing dependency";
            return;
        }

        string outputFolder = Path.GetDirectoryName(video.FilePath)!;
        Directory.CreateDirectory(outputFolder);

        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            WorkingDirectory = outputFolder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        string outputFileName = video.UseOrderNumeration ? $"{video.Order}_{video.FileId}" : video.FileId;
        string? browser = DefaultBrowserCookies.GetBrowserName();

        string[] arguments =
        [
            video.VideoId,
            "--js-runtimes", $"node:{nodePath}",
            "--ffmpeg-location", ffmpegPath,
            "--remote-components", "ejs:github",
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
        DefaultBrowserCookies.AddTo(startInfo, browser);

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => UpdateProgress(video, e.Data);
        process.ErrorDataReceived += (_, e) => UpdateProgress(video, e.Data);

        try
        {
            // First, extract metadata
            await ExtractMetadata(video, ytDlpPath, nodePath, ffmpegPath, browser);

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

    private static async Task ExtractMetadata(Video video, string ytDlpPath, string nodePath, string ffmpegPath, string? browser)
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
