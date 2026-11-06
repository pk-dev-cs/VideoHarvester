using Velopack;
using Velopack.Sources;

namespace VideoHarvester.Services;

public interface IAppUpdateService
{
    bool IsInstalled { get; }
    bool HasDownloadedUpdate { get; }
    Task<string?> CheckAsync();
    Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken);
    void ApplyAndRestart();
}

public sealed class AppUpdateService : IAppUpdateService
{
    public const string ReleasesUrl = "https://github.com/pk-dev-cs/VideoHarvester/releases";
    private readonly UpdateManager _manager;
    private UpdateInfo? _availableUpdate;
    private VelopackAsset? _downloadedUpdate;

    public AppUpdateService() : this(new UpdateManager(new GithubSource("https://github.com/pk-dev-cs/VideoHarvester", null, false)))
    {
    }

    internal AppUpdateService(UpdateManager manager) => _manager = manager;

    public bool IsInstalled => _manager.IsInstalled;
    public bool HasDownloadedUpdate => _downloadedUpdate is not null;

    public async Task<string?> CheckAsync()
    {
        _downloadedUpdate ??= _manager.UpdatePendingRestart;
        if (_downloadedUpdate is not null)
            return _downloadedUpdate.Version.ToString();

        _availableUpdate = await _manager.CheckForUpdatesAsync();
        return _availableUpdate?.TargetFullRelease.Version.ToString();
    }

    public async Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
    {
        if (_downloadedUpdate is not null)
            return;

        var update = _availableUpdate ?? throw new InvalidOperationException("Check for an update before downloading it.");
        await _manager.DownloadUpdatesAsync(update, progress.Report, cancellationToken);
        _downloadedUpdate = update.TargetFullRelease;
    }

    public void ApplyAndRestart()
    {
        var update = _downloadedUpdate ?? throw new InvalidOperationException("Download the update before installing it.");
        _manager.ApplyUpdatesAndRestart(update);
    }
}
