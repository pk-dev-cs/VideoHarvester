using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.ComponentModel;
using VideoHarvester.Localization;
using VideoHarvester.Services;

namespace VideoHarvester;

public partial class AppUpdatesViewModel : ObservableObject, IDisposable
{
    private bool _startupChecked;
    private readonly IAppUpdateService _service;
    private readonly Func<bool> _canRestart;
    private string? _statusKey;
    private object?[] _statusArguments = [];

    public AppUpdatesViewModel(IAppUpdateService service, Func<bool> canRestart)
    {
        _service = service;
        _canRestart = canRestart;
        LocalizationManager.Current.PropertyChanged += OnLanguageChanged;
    }

    [ObservableProperty]
    private string status = string.Empty;

    [ObservableProperty]
    private bool isPanelVisible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadUpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaterCommand))]
    private bool isBusy;

    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private int downloadProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    [NotifyPropertyChangedFor(nameof(DismissButtonText))]
    [NotifyCanExecuteChangedFor(nameof(DownloadUpdateCommand))]
    private string? availableVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    [NotifyCanExecuteChangedFor(nameof(DownloadUpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    private bool isDownloaded;

    public bool ShowDownloadButton => AvailableVersion is not null && !IsDownloaded;
    public bool HasUpdate => AvailableVersion is not null;
    public string DismissButtonText => LocalizationManager.Current[HasUpdate ? "Common_Later" : "Common_Close"];
    private bool CanCheck => !IsBusy;
    private bool CanDownload => !IsBusy && ShowDownloadButton;
    private bool CanInstall => !IsBusy && IsDownloaded;

    public async Task CheckOnStartupAsync()
    {
        if (_startupChecked)
            return;

        _startupChecked = true;
        await CheckAsync(true);
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckForUpdates() => CheckAsync(false);

    private async Task CheckAsync(bool silent)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            if (!_service.IsInstalled)
            {
                if (!silent)
                {
                    SetStatus("Updates_RequiresPackage");
                    IsPanelVisible = true;
                }
                return;
            }

            if (IsDownloaded)
            {
                SetStatus("Updates_Ready", AvailableVersion);
                IsPanelVisible = true;
                return;
            }

            if (!silent)
            {
                SetStatus("Updates_Checking");
                IsPanelVisible = true;
            }

            AvailableVersion = await _service.CheckAsync();
            if (AvailableVersion is not null)
            {
                IsDownloaded = _service.HasDownloadedUpdate;
                SetStatus(IsDownloaded ? "Updates_Ready" : "Updates_Available", AvailableVersion);
                IsPanelVisible = true;
            }
            else if (!silent)
                SetStatus("Updates_Current");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Update check failed: {ex}");
            if (!silent)
            {
                SetStatus("Updates_CheckFailed");
                IsPanelVisible = true;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadUpdate()
    {
        IsBusy = true;
        IsDownloading = true;
        DownloadProgress = 0;
        SetStatus("Updates_Downloading", AvailableVersion);
        try
        {
            await _service.DownloadAsync(new Progress<int>(progress => DownloadProgress = progress), CancellationToken.None);
            IsDownloaded = true;
            SetStatus("Updates_Ready", AvailableVersion);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Update download failed: {ex}");
            SetStatus("Updates_DownloadFailed");
        }
        finally
        {
            IsDownloading = false;
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private void InstallUpdate()
    {
        if (!_canRestart())
        {
            SetStatus("Updates_UnfinishedQueue");
            return;
        }

        IsBusy = true;
        try
        {
            _service.ApplyAndRestart();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Update installation failed: {ex}");
            SetStatus("Updates_InstallFailed");
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private void Later() => IsPanelVisible = false;

    [RelayCommand]
    private void ShowUpdate() => IsPanelVisible = true;

    [RelayCommand]
    private void OpenReleases() => Process.Start(new ProcessStartInfo(AppUpdateService.ReleasesUrl) { UseShellExecute = true });

    private void SetStatus(string key, params object?[] arguments)
    {
        _statusKey = key;
        _statusArguments = arguments;
        Status = LocalizationManager.Current.Format(key, arguments);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(LocalizationManager.Culture))
            return;

        OnPropertyChanged(nameof(DismissButtonText));
        if (_statusKey is not null)
            Status = LocalizationManager.Current.Format(_statusKey, _statusArguments);
    }

    public void Dispose() => LocalizationManager.Current.PropertyChanged -= OnLanguageChanged;
}
