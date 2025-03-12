using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.IO;
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
    private string videoId = "";

    public ObservableCollection<Video> DownloadQueue { get; } = [];

    public MainWindowViewModel(IDownloadVideoService downloadVideoService) 
        => _downloadVideoService = downloadVideoService;

    [RelayCommand]
    public void AddToQueue()
    {
        VideoId = VideoId.Trim();

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
    public void OpenFolder(string filePath)
    {
        string? folderPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(folderPath))
            System.Diagnostics.Process.Start("explorer.exe", folderPath);
    }

    private async void ProcessQueue()
    {
        if (_isDownloading)
            return; // Prevent multiple executions

        _isDownloading = true;

        while (DownloadQueue.Any(v => v.Status == "Queued")) // Process until all are downloaded
        {
            var video = DownloadQueue.First(v => v.Status == "Queued"); // Get the first queued video
            await _downloadVideoService.DownloadVideo(video); // Download and update UI
        }

        _isDownloading = false;
    }
}
