using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HtmlAgilityPack;
using System.Collections.ObjectModel;
using System.IO;
using System.Web;
using System.Windows;
using VideoHarvester.Messages;
using VideoHarvester.Models;
using VideoHarvester.Services;

namespace VideoHarvester;

public partial class MainWindowViewModel : ObservableObject
{
    private static int Order = 0;

    private readonly IDownloadVideoService _downloadVideoService;
    private bool _isDownloading = false;

    [ObservableProperty]
    private string? videoId = "";

    public ObservableCollection<Video> DownloadQueue { get; } = [];

    public MainWindowViewModel(IDownloadVideoService downloadVideoService)
        => _downloadVideoService = downloadVideoService;

    public static string? ExtractWistiaVideoId(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Look for all anchor tags with href attributes
        var links = doc.DocumentNode.SelectNodes("//a[@href]");

        if (links == null) return null;

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", null!);
            if (href != null && href.Contains("wvideo="))
            {
                try
                {
                    var uri = new Uri(href);
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    return query["wvideo"];
                }
                catch
                {
                    // fallback if Uri fails (maybe it's a relative URL)
                    var query = href.Split('?').Skip(1).FirstOrDefault();
                    if (query != null)
                    {
                        var parts = HttpUtility.ParseQueryString(query);
                        return parts["wvideo"];
                    }
                }
            }
        }

        return null;
    }

    [RelayCommand]
    public void AddToQueue()
    {
        VideoId = ExtractWistiaVideoId(VideoId);

        if (string.IsNullOrWhiteSpace(VideoId))
        {
            MessageBox.Show("Please enter a valid VIDEO-ID.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var video = new Video { Order = ++Order, VideoId = VideoId, Progress = 0 };
        DownloadQueue.Add(video);

        WeakReferenceMessenger.Default.Send(new FolderOpenedMessage());

        if (!_isDownloading)
            ProcessQueue();
    }

    [RelayCommand]
    public static void OpenFolder(string filePath)
    {
        string? folderPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(folderPath))
            System.Diagnostics.Process.Start("explorer.exe", folderPath);
    }

    private async void ProcessQueue()
    {
        if (_isDownloading)
            return;

        _isDownloading = true;

        while (DownloadQueue.Any(v => v.Status == "Queued"))
        {
            var video = DownloadQueue.First(v => v.Status == "Queued");
            await _downloadVideoService.DownloadVideo(video);
        }

        _isDownloading = false;
    }
}
