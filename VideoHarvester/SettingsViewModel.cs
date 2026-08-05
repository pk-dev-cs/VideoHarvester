using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoHarvester.Models;
using VideoHarvester.Services;
using Forms = System.Windows.Forms;

namespace VideoHarvester;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _defaultDownloadPath = string.Empty;

    [ObservableProperty]
    private bool _autoStartDownload;

    [ObservableProperty]
    private bool _showNotifications;

    [ObservableProperty]
    private int _maxConcurrentDownloads;

    [ObservableProperty]
    private string _preferredVideoQuality = "Best";

    [ObservableProperty]
    private bool _isSaving;

    public IReadOnlyList<string> VideoQualities { get; } = new[] { "Best", "1080p", "720p", "480p", "360p" };

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _ = LoadSettings();
    }

    private async Task LoadSettings()
    {
        var settings = await _settingsService.LoadSettingsAsync();
        DefaultDownloadPath = settings.DefaultDownloadPath;
        AutoStartDownload = settings.AutoStartDownload;
        ShowNotifications = settings.ShowNotifications;
        MaxConcurrentDownloads = settings.MaxConcurrentDownloads;
        PreferredVideoQuality = settings.PreferredVideoQuality;
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        IsSaving = true;

        var settings = new AppSettings
        {
            DefaultDownloadPath = DefaultDownloadPath,
            AutoStartDownload = AutoStartDownload,
            ShowNotifications = ShowNotifications,
            MaxConcurrentDownloads = MaxConcurrentDownloads,
            PreferredVideoQuality = PreferredVideoQuality
        };

        await _settingsService.SaveSettingsAsync(settings);
        await Task.Delay(500); // Visual feedback

        IsSaving = false;
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select download folder",
            UseDescriptionForTitle = true,
            SelectedPath = DefaultDownloadPath
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            DefaultDownloadPath = dialog.SelectedPath;
        }
    }
}
