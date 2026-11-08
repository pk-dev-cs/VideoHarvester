using VideoHarvester.Localization;
using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.ViewModels;

public class AppUpdatesViewModelTests
{
    [Fact]
    public async Task CheckShowsRequirementForPortableBuild()
    {
        var service = new FakeUpdateService { IsInstalled = false };
        using var viewModel = new AppUpdatesViewModel(service, () => true);

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsPanelVisible);
        Assert.Equal(LocalizationManager.Current["Updates_RequiresPackage"], viewModel.Status);
        Assert.Equal(0, service.CheckCalls);
    }

    [Fact]
    public async Task CheckExposesAvailableUpdate()
    {
        var service = new FakeUpdateService { Version = "0.7.0" };
        using var viewModel = new AppUpdatesViewModel(service, () => true);

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("0.7.0", viewModel.AvailableVersion);
        Assert.True(viewModel.HasUpdate);
        Assert.True(viewModel.ShowDownloadButton);
        Assert.True(viewModel.IsPanelVisible);
    }

    [Fact]
    public async Task StartupCheckRunsOnlyOnce()
    {
        var service = new FakeUpdateService();
        using var viewModel = new AppUpdatesViewModel(service, () => true);

        await viewModel.CheckOnStartupAsync();
        await viewModel.CheckOnStartupAsync();

        Assert.Equal(1, service.CheckCalls);
    }

    [Fact]
    public async Task DownloadMarksUpdateReady()
    {
        var service = new FakeUpdateService { Version = "0.7.0" };
        using var viewModel = new AppUpdatesViewModel(service, () => true);
        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        await viewModel.DownloadUpdateCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsDownloaded);
        Assert.False(viewModel.IsDownloading);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(1, service.DownloadCalls);
        Assert.Equal(LocalizationManager.Current.Format("Updates_Ready", "0.7.0"), viewModel.Status);
    }

    [Fact]
    public async Task InstallIsBlockedByUnfinishedQueue()
    {
        var service = new FakeUpdateService { Version = "0.7.0" };
        using var viewModel = new AppUpdatesViewModel(service, () => false);
        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);
        await viewModel.DownloadUpdateCommand.ExecuteAsync(null);

        viewModel.InstallUpdateCommand.Execute(null);

        Assert.Equal(0, service.ApplyCalls);
        Assert.Equal(LocalizationManager.Current["Updates_UnfinishedQueue"], viewModel.Status);
    }

    [Fact]
    public async Task InstallDelegatesWhenQueueCanRestart()
    {
        var service = new FakeUpdateService { Version = "0.7.0" };
        using var viewModel = new AppUpdatesViewModel(service, () => true);
        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);
        await viewModel.DownloadUpdateCommand.ExecuteAsync(null);

        viewModel.InstallUpdateCommand.Execute(null);

        Assert.Equal(1, service.ApplyCalls);
    }

    private sealed class FakeUpdateService : IAppUpdateService
    {
        public bool IsInstalled { get; init; } = true;
        public bool HasDownloadedUpdate { get; set; }
        public string? Version { get; init; }
        public int CheckCalls { get; private set; }
        public int DownloadCalls { get; private set; }
        public int ApplyCalls { get; private set; }

        public Task<string?> CheckAsync()
        {
            CheckCalls++;
            return Task.FromResult(Version);
        }

        public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
        {
            DownloadCalls++;
            HasDownloadedUpdate = true;
            progress.Report(100);
            return Task.CompletedTask;
        }

        public void ApplyAndRestart() => ApplyCalls++;
    }
}
