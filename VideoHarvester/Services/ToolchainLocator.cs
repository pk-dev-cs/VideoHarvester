using System.IO;

namespace VideoHarvester.Services;

internal static class ToolchainLocator
{
    private static readonly string ToolsDirectory = Path.Combine(AppContext.BaseDirectory, "tools");

    public static string? FindYtDlp() => FindExecutable("yt-dlp.exe");

    public static string? FindFFmpeg() => FindExecutable("ffmpeg.exe");

    public static string? FindFFprobe() => FindExecutable("ffprobe.exe");

    public static string? FindNode()
    {
        string? path = FindExecutable("node.exe");
        if (path is not null)
            return path;

        string[] installationPaths =
        [
            @"C:\Program Files\nodejs\node.exe",
            @"C:\Program Files (x86)\nodejs\node.exe"
        ];

        return installationPaths.FirstOrDefault(File.Exists);
    }

    private static string? FindExecutable(string fileName)
    {
        string bundledPath = Path.Combine(ToolsDirectory, fileName);
        if (File.Exists(bundledPath))
            return bundledPath;

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
            return null;

        foreach (string folder in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(folder.Trim(), fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (Exception)
            {
            }
        }

        return null;
    }
}
