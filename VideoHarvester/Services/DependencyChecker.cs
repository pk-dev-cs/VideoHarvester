using System.Diagnostics;
using System.IO;
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

        string? nodePath = FindNode();
        status.IsAvailable = nodePath is not null;
        return status;
    }

    public static async Task<DependencyStatus> CheckPython()
    {
        var status = new DependencyStatus
        {
            Name = "Python",
            Icon = "\uE943" // Python-like code icon
        };

        status.IsAvailable = await CommandExists("python", "--version");
        return status;
    }

    public static async Task<DependencyStatus> CheckFFmpeg()
    {
        var status = new DependencyStatus
        {
            Name = "FFmpeg",
            Icon = "\uE8B8" // Video icon
        };

        status.IsAvailable = await CommandExists("ffmpeg", "-version");
        return status;
    }

    public static async Task<DependencyStatus> CheckYtDlp()
    {
        var status = new DependencyStatus
        {
            Name = "yt-dlp",
            Icon = "\uE896" // Download icon
        };

        // Check if yt-dlp is available via python -m yt_dlp
        status.IsAvailable = await CheckPythonModule();
        return status;
    }

    private static async Task<bool> CheckPythonModule()
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
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add("yt_dlp");
            startInfo.ArgumentList.Add("--version");

            using Process? process = Process.Start(startInfo);
            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception) { return false; }
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
}
