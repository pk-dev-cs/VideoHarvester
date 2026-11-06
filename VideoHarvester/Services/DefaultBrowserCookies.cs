using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace VideoHarvester.Services;

internal static class DefaultBrowserCookies
{
    public static string? GetBrowserName()
    {
        using var choice = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
        string? programId = choice?.GetValue("ProgId") as string;
        if (string.IsNullOrWhiteSpace(programId))
            return null;

        using var commandKey = Registry.ClassesRoot.OpenSubKey($@"{programId}\shell\open\command");
        string? command = commandKey?.GetValue(null) as string;
        if (string.IsNullOrWhiteSpace(command))
            return null;

        Match match = Regex.Match(command, "^\\s*(?:\"(?<path>[^\"]+\\.exe)\"|(?<path>\\S+\\.exe))", RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        string executable = match.Groups["path"].Value;
        return Path.GetFileName(executable).ToLowerInvariant() switch
        {
            "firefox.exe" => "firefox",
            "chrome.exe" => "chrome",
            "msedge.exe" => "edge",
            "brave.exe" => "brave",
            "chromium.exe" => "chromium",
            "opera.exe" => "opera",
            "launcher.exe" when executable.Contains("Opera", StringComparison.OrdinalIgnoreCase) => "opera",
            "vivaldi.exe" => "vivaldi",
            "whale.exe" => "whale",
            _ => null
        };
    }

    public static void AddTo(ProcessStartInfo startInfo, string? browser)
    {
        if (browser is null)
            return;

        startInfo.ArgumentList.Add("--cookies-from-browser");
        startInfo.ArgumentList.Add(browser);
    }
}
