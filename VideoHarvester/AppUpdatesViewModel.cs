using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using VideoHarvester.Services;

namespace VideoHarvester;

public partial class AppUpdatesViewModel(IAppUpdateService service, Func<bool> canRestart) : ObservableObject
{
    private bool _startupChecked;

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
    [NotifyCanExecuteChangedFor(nameof(DownloadUpdateCommand))]
    private string? availableVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    [NotifyCanExecuteChangedFor(nameof(DownloadUpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    private bool isDownloaded;

    public bool ShowDownloadButton => AvailableVersion is not null && !IsDownloaded;
    public bool HasUpdate => AvailableVersion is not null;
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
            if (!service.IsInstalled)
            {
                if (!silent)
                {
                    Status = "To enable automatic updates, use the installer or portable package from GitHub Releases.";
                    IsPanelVisible = true;
                }
                return;
            }

            if (IsDownloaded)
            {
                Status = $"Version {AvailableVersion} is ready to install.";
                IsPanelVisible = true;
                return;
            }

            if (!silent)
            {
                Status = "Checking for updates…";
                IsPanelVisible = true;
            }

            AvailableVersion = await service.CheckAsync();
            if (AvailableVersion is not null)
            {
                IsDownloaded = service.HasDownloadedUpdate;
                Status = IsDownloaded ? $"Version {AvailableVersion} is ready to install." : $"Version {AvailableVersion} is available.";
                IsPanelVisible = true;
            }
            else if (!silent)
                Status = "You're up to date.";
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Update check failed: {ex}");
            if (!silent)
            {
                Status = "Could not check for updates. Try again later or visit GitHub Releases.";
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
        Status = $"Downloading version {AvailableVersion}…";
        try
        {
            await service.DownloadAsync(new Progress<int>(progress => DownloadProgress = progress), CancellationToken.None);
            IsDownloaded = true;
            Status = $"Version {AvailableVersion} is ready to install.";
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Update download failed: {ex}");
            Status = "Could not download the update. Your current version is unchanged. Try again later.";
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
        if (!canRestart())
        {
            Status = "Finish or remove all unfinished queue items before updating. The update is ready when you are.";
            return;
        }

        IsBusy = true;
        try
        {
            service.ApplyAndRestart();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Update installation failed: {ex}");
            Status = "Could not install the update. You can keep using this version and try again later.";
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private void Later() => IsPanelVisible = false;

    [RelayCommand]
    private void ShowUpdate() => IsPanelVisible = true;

    [RelayCommand]
    private void OpenReleases() => Process.Start(new ProcessStartInfo(AppUpdateService.ReleasesUrl) { UseShellExecute = true });
}
