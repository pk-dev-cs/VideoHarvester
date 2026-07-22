using VideoHarvester.Services;

namespace VideoHarvester.Utilities;

/// <summary>
/// Helper class for testing and demonstrating download history functionality
/// </summary>
public static class HistoryHelper
{
    /// <summary>
    /// Displays all downloads in the console (for testing purposes)
    /// </summary>
    public static async Task DisplayAllDownloads(IDownloadHistoryService historyService)
    {
        var downloads = await historyService.GetAllDownloadsAsync();

        Console.WriteLine($"Total downloads: {downloads.Count}");
        Console.WriteLine(new string('-', 80));

        foreach (var download in downloads)
        {
            Console.WriteLine($"ID: {download.Id}");
            Console.WriteLine($"URL: {download.SourceUrl}");
            Console.WriteLine($"Title: {download.Title ?? "N/A"}");
            Console.WriteLine($"Status: {download.Status}");
            Console.WriteLine($"Format: {download.Format ?? "N/A"}");
            Console.WriteLine($"Size: {FormatFileSize(download.FileSize)}");
            Console.WriteLine($"Created: {download.CreatedAt}");
            Console.WriteLine($"Finished: {download.FinishedAt?.ToString() ?? "N/A"}");
            if (!string.IsNullOrEmpty(download.ErrorMessage))
            {
                Console.WriteLine($"Error: {download.ErrorMessage}");
            }
            Console.WriteLine(new string('-', 80));
        }
    }

    /// <summary>
    /// Gets download statistics
    /// </summary>
    public static async Task<(int Total, int Completed, int Failed, long TotalSize)> GetStatistics(IDownloadHistoryService historyService)
    {
        var downloads = await historyService.GetAllDownloadsAsync();

        var total = downloads.Count;
        var completed = downloads.Count(d => d.Status == "Completed");
        var failed = downloads.Count(d => d.Status == "Failed");
        var totalSize = downloads.Where(d => d.FileSize.HasValue).Sum(d => d.FileSize!.Value);

        return (total, completed, failed, totalSize);
    }

    private static string FormatFileSize(long? bytes)
    {
        if (!bytes.HasValue) return "N/A";

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes.Value;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
