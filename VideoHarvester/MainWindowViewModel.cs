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

    [ObservableProperty]
    private VideoSource selectedSource = VideoSource.Wistia;

    public IReadOnlyList<VideoSource> AvailableSources { get; } = Enum.GetValues<VideoSource>();

    public ObservableCollection<Video> DownloadQueue { get; } = [];

    public ObservableCollection<DependencyStatus> Dependencies { get; } = [];

    public MainWindowViewModel(IDownloadVideoService downloadVideoService)
    {
        _downloadVideoService = downloadVideoService;
        _ = InitializeDependencies();
    }

    private async Task InitializeDependencies()
    {
        Dependencies.Add(await DependencyChecker.CheckNodeJs());
        Dependencies.Add(await DependencyChecker.CheckPython());
        Dependencies.Add(await DependencyChecker.CheckFFmpeg());
        Dependencies.Add(await DependencyChecker.CheckYtDlp());
    }

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
        string? videoReference = SelectedSource == VideoSource.Wistia ? ExtractWistiaVideoId(VideoId) : NormalizeYouTubeUrl(VideoId);

        if (string.IsNullOrWhiteSpace(videoReference))
        {
            MessageBox.Show(SelectedSource == VideoSource.Wistia ? "Please enter a page address containing a Wistia video." : "Please enter a valid YouTube video URL.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var video = new Video { Order = ++Order, VideoId = videoReference, Source = SelectedSource, Progress = 0 };
        DownloadQueue.Add(video);

        WeakReferenceMessenger.Default.Send(new FolderOpenedMessage());

        if (!_isDownloading)
            ProcessQueue();
    }

    private static string? NormalizeYouTubeUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri? uri)) return null;
        string host = uri.Host.ToLowerInvariant();
        bool valid = host == "youtu.be" || host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal) || host == "youtube-nocookie.com" || host.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal);
        return valid ? uri.AbsoluteUri : null;
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
