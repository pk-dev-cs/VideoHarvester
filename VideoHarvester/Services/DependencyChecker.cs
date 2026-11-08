using System.Diagnostics;
using VideoHarvester.Models;

namespace VideoHarvester.Services;

internal static class DependencyChecker
{
    public static async Task<DependencyStatus> CheckNodeJs()
    {
        var status = new DependencyStatus
        {
            Name = "Node.js",
            Icon = "\uE774" // Computer icon from Segoe MDL2 Assets
        };

        (status.IsAvailable, status.Version) = await ReadVersion(ToolchainLocator.FindNode(), "--version");
        return status;
    }

    public static async Task<DependencyStatus> CheckFFmpeg()
    {
        var status = new DependencyStatus
        {
            Name = "FFmpeg",
            Icon = "\uE8B8" // Video icon
        };

        (status.IsAvailable, status.Version) = await ReadVersion(ToolchainLocator.FindFFmpeg(), "-version", "ffmpeg version ");
        return status;
    }

    public static async Task<DependencyStatus> CheckYtDlp()
    {
        var status = new DependencyStatus
        {
            Name = "yt-dlp",
            Icon = "\uE896" // Download icon
        };

        (status.IsAvailable, status.Version) = await ReadVersion(ToolchainLocator.FindYtDlp(), "--version");
        return status;
    }

    private static async Task<(bool IsAvailable, string? Version)> ReadVersion(string? command, string argument, string? versionPrefix = null)
    {
        if (command is null)
            return (false, null);

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
            startInfo.ArgumentList.Add(argument);
            using Process? process = Process.Start(startInfo);
            if (process is null)
                return (false, null);

            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string output = string.Join(Environment.NewLine, await standardOutput, await standardError);
            string? versionLine = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (versionPrefix is not null && versionLine?.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase) == true)
                versionLine = versionLine[versionPrefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            return (process.ExitCode == 0, versionLine);
        }
        catch (Exception)
        {
            return (false, null);
        }
    }
}
