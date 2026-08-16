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
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace VideoHarvester;

public partial class MainWindowViewModel : ObservableObject
{
    private static int Order = 0;

    private readonly IDownloadVideoService _downloadVideoService;
    private readonly IDownloadHistoryService _historyService;
    private readonly ISettingsService _settingsService;
    private bool _isDownloading = false;
    private readonly Dictionary<Video, CancellationTokenSource> _downloadCancellations = new();
    private readonly HashSet<Video> _pauseRequests = [];
    private readonly SemaphoreSlim _downloadSemaphore;
    private int _currentMaxConcurrent = 1;
    private readonly Task _semaphoreInitialization;
    private TaskCompletionSource<bool> _queueChangedSignal = new();

    [ObservableProperty]
    private string? videoId = "";

    [ObservableProperty]
    private VideoSource selectedSource = VideoSource.Wistia;

    [ObservableProperty]
    private bool isQueueView = true;

    [ObservableProperty]
    private bool isSettingsView = false;

    [ObservableProperty]
    private bool useOrderNumeration = false;

    public IReadOnlyList<VideoSource> AvailableSources { get; } = Enum.GetValues<VideoSource>();

    public ObservableCollection<Video> DownloadQueue { get; } = [];

    public ObservableCollection<DownloadHistory> DownloadHistory { get; } = [];

    public ObservableCollection<DependencyStatus> Dependencies { get; } = [];

    public SettingsViewModel Settings { get; }

    public MainWindowViewModel(IDownloadVideoService downloadVideoService, IDownloadHistoryService historyService, ISettingsService settingsService, SettingsViewModel settingsViewModel)
    {
        _downloadVideoService = downloadVideoService;
        _historyService = historyService;
        _settingsService = settingsService;
        Settings = settingsViewModel;
        _downloadSemaphore = new SemaphoreSlim(1, 5);
        _ = InitializeDependencies();
        _semaphoreInitialization = InitializeSemaphore();

        // Subscribe to settings updates
        WeakReferenceMessenger.Default.Register<SettingsUpdatedMessage>(this, (r, m) =>
        {
            UpdateSemaphoreLimit(m.MaxConcurrentDownloads);
        });
    }

    private async Task InitializeSemaphore()
    {
        var settings = await _settingsService.LoadSettingsAsync();
        var maxConcurrent = Math.Clamp(settings.MaxConcurrentDownloads, 1, 5);
        _currentMaxConcurrent = maxConcurrent;
        _downloadSemaphore.Release(maxConcurrent - 1);
    }

    private void UpdateSemaphoreLimit(int newMaxConcurrent)
    {
        newMaxConcurrent = Math.Clamp(newMaxConcurrent, 1, 5);
        int difference = newMaxConcurrent - _currentMaxConcurrent;

        System.Diagnostics.Debug.WriteLine($"UpdateSemaphoreLimit: current={_currentMaxConcurrent}, new={newMaxConcurrent}, diff={difference}");

        if (difference > 0)
        {
            _downloadSemaphore.Release(difference);
            System.Diagnostics.Debug.WriteLine($"Released {difference} slots");
        }
        else if (difference < 0)
        {
            for (int i = 0; i < Math.Abs(difference); i++)
            {
                _downloadSemaphore.WaitAsync(0).ConfigureAwait(false);
            }
            System.Diagnostics.Debug.WriteLine($"Acquired {Math.Abs(difference)} slots");
        }

        _currentMaxConcurrent = newMaxConcurrent;
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
        string? videoReference = SelectedSource switch
        {
            VideoSource.Wistia => ExtractWistiaVideoId(VideoId),
            VideoSource.Cda => ExtractCdaVideoId(VideoId),
            _ => NormalizeYouTubeUrl(VideoId)
        };

        if (string.IsNullOrWhiteSpace(videoReference))
        {
            string errorMessage = SelectedSource switch
            {
                VideoSource.Wistia => "Please enter a page address containing a Wistia video.",
                VideoSource.Cda => "Please enter a valid Cda.pl video URL (e.g., https://www.cda.pl/video/3383563ba).",
                _ => "Please enter a valid YouTube video URL."
            };

            System.Windows.MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var video = new Video
        {
            Order = ++Order,
            VideoId = videoReference,
            Source = SelectedSource,
            Progress = 0,
            UseOrderNumeration = UseOrderNumeration,
            PreferredQuality = Settings.PreferredVideoQuality,
            DownloadDirectory = ResolveDownloadDirectory(Settings.DefaultDownloadPath)
        };

        if (!Settings.AutoStartDownload)
            video.Status = "Ready";

        DownloadQueue.Add(video);

        WeakReferenceMessenger.Default.Send(new FolderOpenedMessage());

        if (Settings.AutoStartDownload)
            StartQueueProcessing();
    }

    private static string? NormalizeYouTubeUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri? uri)) return null;
        string host = uri.Host.ToLowerInvariant();
        bool valid = host == "youtu.be" || host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal) || host == "youtube-nocookie.com" || host.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal);
        return valid ? uri.AbsoluteUri : null;
    }

    private static string? ExtractCdaVideoId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Try to parse as URL first
        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri))
        {
            string host = uri.Host.ToLowerInvariant();
            if (host == "cda.pl" || host.EndsWith(".cda.pl", StringComparison.Ordinal))
            {
                // Extract video ID from path like /video/3383563ba
                var match = System.Text.RegularExpressions.Regex.Match(uri.AbsolutePath, @"/video/([a-zA-Z0-9]+)");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }

        // If not a URL, maybe it's just the video ID
        var idMatch = System.Text.RegularExpressions.Regex.Match(value.Trim(), @"^([a-zA-Z0-9]+)$");
        if (idMatch.Success)
        {
            return idMatch.Groups[1].Value;
        }

        return null;
    }

    private static string ResolveDownloadDirectory(string? configuredPath)
    {
        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "VideoHarvester")
            : Path.GetFullPath(configuredPath.Trim());
    }

    [RelayCommand]
    public static void OpenFolder(string filePath)
    {
        string? folderPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(folderPath))
            System.Diagnostics.Process.Start("explorer.exe", folderPath);
    }

    [RelayCommand]
    public void RemoveFromQueue(Video video)
    {
        _pauseRequests.Remove(video);

        if (_downloadCancellations.TryGetValue(video, out var cts))
            cts.Cancel();

        DownloadQueue.Remove(video);
    }

    [RelayCommand]
    public void StartDownload(Video video)
    {
        if (video.Status == "Downloaded" || _downloadCancellations.ContainsKey(video))
            return;

        _pauseRequests.Remove(video);
        video.ErrorMessage = null;
        video.Status = "Queued";
        StartQueueProcessing();
    }

    [RelayCommand]
    public void PauseDownload(Video video)
    {
        if (_downloadCancellations.TryGetValue(video, out var cts))
        {
            _pauseRequests.Add(video);
            video.Status = "Pausing";
            cts.Cancel();
            return;
        }

        if (video.Status == "Queued")
            video.Status = "Paused";
    }

    [RelayCommand]
    public void StopDownload(Video video)
    {
        _pauseRequests.Remove(video);

        if (_downloadCancellations.TryGetValue(video, out var cts))
        {
            video.Status = "Stopping";
            cts.Cancel();
            return;
        }

        if (video.Status != "Downloaded")
            video.Status = "Cancelled";
    }

    private void StartQueueProcessing()
    {
        if (!_isDownloading)
        {
            ProcessQueue();
            return;
        }

        var signal = _queueChangedSignal;
        _queueChangedSignal = new TaskCompletionSource<bool>();
        signal.TrySetResult(true);
    }
    private async void ProcessQueue()
    {
        if (_isDownloading)
            return;

        _isDownloading = true;

        // Wait for semaphore initialization to complete
        await _semaphoreInitialization;
        System.Diagnostics.Debug.WriteLine($"Semaphore initialized with {_currentMaxConcurrent} slots");

        var downloadTasks = new List<Task>();

        try
        {
            while (true)
            {
                // Clean up completed tasks
                downloadTasks.RemoveAll(t => t.IsCompleted);
                System.Diagnostics.Debug.WriteLine($"Active tasks: {downloadTasks.Count}, Queued videos: {DownloadQueue.Count(v => v.Status == "Queued")}");

                // Check if we can start new downloads
                var queuedVideos = DownloadQueue.Where(v => v.Status == "Queued").ToList();

                foreach (var video in queuedVideos)
                {
                    // Try to acquire semaphore without blocking
                    bool acquired = await _downloadSemaphore.WaitAsync(0);
                    System.Diagnostics.Debug.WriteLine($"Trying to start {video.VideoId}: {(acquired ? "SUCCESS" : "FAILED - no slots")}");

                    if (!acquired)
                        break; // No available slots right now

                    var cts = new CancellationTokenSource();
                    _downloadCancellations[video] = cts;
                    video.Status = "Starting";

                    // Capture video reference for the task
                    var currentVideo = video;
                    var currentCts = cts;

                    var downloadTask = DownloadVideoAsync(currentVideo, currentCts);
                    downloadTasks.Add(downloadTask);
                }

                // If no tasks running and no queued items, we're done
                if (!downloadTasks.Any() && !DownloadQueue.Any(v => v.Status == "Queued"))
                    break;

                // If we have running tasks, wait for at least one to complete OR for a new item signal
                if (downloadTasks.Any())
                {
                    var tasksWithSignal = new List<Task>(downloadTasks) { _queueChangedSignal.Task };
                    var completedTask = await Task.WhenAny(tasksWithSignal);

                    // If it's the signal, loop will restart and check for new items
                    if (completedTask == _queueChangedSignal.Task)
                    {
                        System.Diagnostics.Debug.WriteLine("New item added to queue - checking for available slots");
                        continue;
                    }

                    // Ensure any exceptions are observed
                    try
                    {
                        await completedTask;
                    }
                    catch
                    {
                        // Exception already handled in DownloadVideoAsync
                    }
                }
                else
                {
                    // No running tasks but there are queued items
                    // This shouldn't happen, but add a small delay to prevent tight loop
                    await Task.Delay(100);
                }
            }
        }
        finally
        {
            // Wait for all remaining downloads to complete
            if (downloadTasks.Any())
            {
                await Task.WhenAll(downloadTasks);
            }

            _isDownloading = false;
        }
    }

    private async Task DownloadVideoAsync(Video video, CancellationTokenSource cts)
    {
        try
        {
            await _downloadVideoService.DownloadVideo(video, cts.Token);
        }
        catch (OperationCanceledException)
        {
            video.Status = _pauseRequests.Remove(video) ? "Paused" : "Cancelled";
        }
        catch (Exception ex)
        {
            // Ensure any unexpected exceptions don't crash the queue processing
            video.Status = "Failed";
            if (string.IsNullOrEmpty(video.ErrorMessage))
            {
                video.ErrorMessage = $"Unexpected error: {ex.Message}";
            }
        }
        finally
        {
            _pauseRequests.Remove(video);
            _downloadCancellations.Remove(video);
            _downloadSemaphore.Release();
        }
    }

    [RelayCommand]
    public async Task LoadHistory()
    {
        DownloadHistory.Clear();
        var history = await _historyService.GetAllDownloadsAsync();
        foreach (var item in history)
        {
            DownloadHistory.Add(item);
        }
    }

    [RelayCommand]
    public async Task RefreshHistory()
    {
        await LoadHistory();
    }

    [RelayCommand]
    public static void CopyErrorToClipboard(string errorMessage)
    {
        if (!string.IsNullOrEmpty(errorMessage))
        {
            System.Windows.Clipboard.SetText(errorMessage);
        }
    }
}
