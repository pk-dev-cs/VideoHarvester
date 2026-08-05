using System.IO;

namespace VideoHarvester.Models;

public class AppSettings
{
    public string DefaultDownloadPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "VideoHarvester");
    public bool AutoStartDownload { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public int MaxConcurrentDownloads { get; set; } = 1;
    public string PreferredVideoQuality { get; set; } = "Best";
}
