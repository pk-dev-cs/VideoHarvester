using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VideoHarvester.Messages;
using VideoHarvester.Models;
using VideoHarvester.Services;
using VideoHarvester.Localization;
using Forms = System.Windows.Forms;

namespace VideoHarvester;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _defaultDownloadPath = string.Empty;

    [ObservableProperty]
    private bool _autoStartDownload = true;

    [ObservableProperty]
    private bool _showNotifications;

    [ObservableProperty]
    private int _maxConcurrentDownloads;

    partial void OnMaxConcurrentDownloadsChanged(int value)
    {
        // Clamp value between 1 and 5
        if (value < 1)
            MaxConcurrentDownloads = 1;
        else if (value > 5)
            MaxConcurrentDownloads = 5;
    }

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
        try
        {
            var settings = new AppSettings
            {
                DefaultDownloadPath = DefaultDownloadPath,
                AutoStartDownload = AutoStartDownload,
                ShowNotifications = ShowNotifications,
                MaxConcurrentDownloads = MaxConcurrentDownloads,
                PreferredVideoQuality = PreferredVideoQuality,
                Language = LocalizationManager.Current.LanguageCode
            };
            await _settingsService.SaveSettingsAsync(settings);
            WeakReferenceMessenger.Default.Send(new SettingsUpdatedMessage
            {
                MaxConcurrentDownloads = MaxConcurrentDownloads
            });
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(LocalizationManager.Current.Format("Error_SaveSettings", ex.Message),
                LocalizationManager.Current["Dialog_Error"], System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = LocalizationManager.Current["Settings_SelectFolder"],
            UseDescriptionForTitle = true,
            SelectedPath = DefaultDownloadPath
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            DefaultDownloadPath = dialog.SelectedPath;
        }
    }
}
