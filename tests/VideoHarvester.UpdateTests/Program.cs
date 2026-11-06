using Microsoft.Data.Sqlite;
using System.IO;
using System.Net.Http;
using Velopack;
using Velopack.Locators;
using VideoHarvester;
using VideoHarvester.Services;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--preview")
        {
            UpdatePreview.Run(args[1] == "blocked");
            return;
        }
        RunAsync(args).GetAwaiter().GetResult();
        UpdateLayoutChecks.Run(args.Length == 2 && args[0] == "--screenshots" ? Path.GetFullPath(args[1]) : null);
    }

    private static async Task RunAsync(string[] args)
    {
        await LocalizationChecks.RunAsync();
        var unavailable = new FakeUpdates { IsInstalled = false };
        var unavailableView = new AppUpdatesViewModel(unavailable, () => true);
        await unavailableView.CheckOnStartupAsync();
        Check(!unavailableView.IsPanelVisible && unavailable.Checks == 0, "Development builds must not check or prompt automatically.");
        await unavailableView.CheckForUpdatesCommand.ExecuteAsync(null);
        Check(unavailableView.IsPanelVisible, "Manual checks must explain when an installer is required.");
        Pass("unpackaged-build");

        var current = new AppUpdatesViewModel(new FakeUpdates { Version = null }, () => true);
        await current.CheckForUpdatesCommand.ExecuteAsync(null);
        Check(current.AvailableVersion is null && !current.ShowDownloadButton && !current.IsDownloaded, "No update must expose no download or restart action.");
        Pass("already-current");

        bool canRestart = false;
        var updates = new FakeUpdates();
        var view = new AppUpdatesViewModel(updates, () => canRestart);
        await view.CheckOnStartupAsync();
        await view.CheckOnStartupAsync();
        Check(updates.Checks == 1 && view.ShowDownloadButton, "Startup checks must run once and expose the new version.");
        await view.DownloadUpdateCommand.ExecuteAsync(null);
        Check(view.IsDownloaded && view.InstallUpdateCommand.CanExecute(null), "A verified download must enable installation.");
        view.LaterCommand.Execute(null);
        Check(!view.IsPanelVisible && view.IsDownloaded && updates.Applies == 0, "Later must preserve the download without applying it.");
        await view.CheckForUpdatesCommand.ExecuteAsync(null);
        Check(view.IsPanelVisible && view.IsDownloaded && updates.Checks == 1, "A deferred update must remain available without another network check.");
        view.InstallUpdateCommand.Execute(null);
        Check(updates.Applies == 0 && !view.IsBusy, "An unfinished queue must block installation.");
        canRestart = true;
        view.InstallUpdateCommand.Execute(null);
        Check(updates.Applies == 1, "An idle, finished queue must allow the confirmed restart.");
        Pass("download-defer-and-queue-guard");

        var pending = new AppUpdatesViewModel(new FakeUpdates { HasDownloadedUpdate = true }, () => true);
        await pending.CheckOnStartupAsync();
        Check(pending.IsDownloaded && !pending.ShowDownloadButton, "A download from the previous session must be ready to install.");
        Pass("pending-update-on-relaunch");

        var failedDownload = new FakeUpdates { FailDownload = true };
        var failedView = new AppUpdatesViewModel(failedDownload, () => true);
        await failedView.CheckForUpdatesCommand.ExecuteAsync(null);
        await failedView.DownloadUpdateCommand.ExecuteAsync(null);
        Check(!failedView.IsDownloaded && !failedView.IsBusy && failedView.DownloadUpdateCommand.CanExecute(null), "A failed download must be retryable and must not allow installation.");
        failedDownload.FailDownload = false;
        await failedView.DownloadUpdateCommand.ExecuteAsync(null);
        failedDownload.FailApply = true;
        failedView.InstallUpdateCommand.Execute(null);
        Check(!failedView.IsBusy && failedView.IsDownloaded && failedView.InstallUpdateCommand.CanExecute(null), "Failed installation must leave the current app usable and allow retry.");
        Pass("download-and-install-errors");

        var offline = new AppUpdatesViewModel(new FakeUpdates { FailCheck = true }, () => true);
        await offline.CheckOnStartupAsync();
        Check(!offline.IsPanelVisible && !offline.IsBusy, "Automatic network errors must not interrupt users.");
        await offline.CheckForUpdatesCommand.ExecuteAsync(null);
        Check(offline.IsPanelVisible && !offline.IsBusy, "Manual network errors must be reported without blocking the app.");
        Pass("offline-checks");

        await TestMigrationAsync();
        if (args.Length == 2 && args[0] == "--feed")
            await TestPackagedUpdateAsync(Path.GetFullPath(args[1]));
    }

    private static async Task TestMigrationAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "VideoHarvester-migration-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string legacy = Path.Combine(root, "legacy.db");
            string migrated = Path.Combine(root, "data", "downloads.db");
            using (var original = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = legacy, Pooling = false }.ToString()))
            {
                await original.OpenAsync();
                var create = original.CreateCommand();
                create.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE downloads (id INTEGER PRIMARY KEY, title TEXT); INSERT INTO downloads VALUES (1, 'Original history');";
                await create.ExecuteNonQueryAsync();
                await HistoryDatabaseMigration.MigrateAsync(legacy, migrated);
                Check(await ReadCountAsync(migrated) == 1, "Migration must include committed WAL data.");
                create.CommandText = "INSERT INTO downloads VALUES (2, 'Added later');";
                await create.ExecuteNonQueryAsync();
                await HistoryDatabaseMigration.MigrateAsync(legacy, migrated);
                Check(await ReadCountAsync(migrated) == 1, "Migration must never overwrite an existing destination.");
                Check(await ReadCountAsync(legacy) == 2, "Migration must leave the original database intact.");
            }
            Pass("history-migration-and-wal");

            string missingDestination = Path.Combine(root, "missing", "downloads.db");
            await HistoryDatabaseMigration.MigrateAsync(Path.Combine(root, "absent.db"), missingDestination);
            Check(!File.Exists(missingDestination), "Missing legacy files must not create an empty migrated database.");
            Pass("no-legacy-history");

            string corrupt = Path.Combine(root, "corrupt.db");
            await File.WriteAllTextAsync(corrupt, "Not a SQLite database");
            string failedDestination = Path.Combine(root, "failed", "downloads.db");
            bool failed = false;
            try
            {
                await HistoryDatabaseMigration.MigrateAsync(corrupt, failedDestination);
            }
            catch (SqliteException)
            {
                failed = true;
            }
            Check(failed && File.Exists(corrupt) && !File.Exists(failedDestination), "Failed migration must preserve the original and leave no final database.");
            Pass("failed-history-migration");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task TestPackagedUpdateAsync(string feed)
    {
        string cache = Path.Combine(Path.GetTempPath(), "VideoHarvester-update-cache-" + Guid.NewGuid());
        Directory.CreateDirectory(cache);
        try
        {
            var locator = new TestVelopackLocator("VideoHarvester", "0.0.1", cache);
            var manager = new UpdateManager(feed, locator: locator);
            var service = new AppUpdateService(manager);
            Check(service.IsInstalled, "The test locator must represent an installed application.");
            string? version = await service.CheckAsync();
            Check(version is not null, "A newly packed release must be found in the local feed.");
            await service.DownloadAsync(new Progress<int>(), CancellationToken.None);
            Check(service.HasDownloadedUpdate && Directory.GetFiles(cache, "*.nupkg").Length > 0, "Velopack must download and verify the actual release package.");
            var sameVersion = new UpdateManager(feed, locator: new TestVelopackLocator("VideoHarvester", version!, Path.Combine(cache, "current")));
            Check(await sameVersion.CheckForUpdatesAsync() is null, "An equal installed version must not offer an update.");
            Pass("real-velopack-package-and-feed");

            string corruptFeed = Path.Combine(cache, "corrupt-feed");
            Directory.CreateDirectory(corruptFeed);
            string feedJson = await File.ReadAllTextAsync(Path.Combine(feed, "releases.win.json"));
            await File.WriteAllTextAsync(Path.Combine(corruptFeed, "releases.win.json"), feedJson);
            var package = VelopackAssetFeed.FromJson(feedJson).Assets.First(asset => asset.Type == VelopackAssetType.Full);
            await File.WriteAllTextAsync(Path.Combine(corruptFeed, package.FileName), "Corrupt update package");
            var corruptService = new AppUpdateService(new UpdateManager(corruptFeed,
                locator: new TestVelopackLocator("VideoHarvester", "0.0.1", Path.Combine(cache, "bad-cache"))));
            await corruptService.CheckAsync();
            bool rejected = false;
            try
            {
                await corruptService.DownloadAsync(new Progress<int>(), CancellationToken.None);
            }
            catch (Velopack.Exceptions.ChecksumFailedException)
            {
                rejected = true;
            }
            Check(rejected && !corruptService.HasDownloadedUpdate, "Corrupt packages must never be marked ready to install.");
            Pass("corrupt-package-rejected");
        }
        finally
        {
            Directory.Delete(cache, true);
        }
    }

    private static async Task<long> ReadCountAsync(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM downloads";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Pass(string scenario) => Console.WriteLine($"PASS {scenario}");

    private sealed class FakeUpdates : IAppUpdateService
    {
        public bool IsInstalled { get; init; } = true;
        public bool HasDownloadedUpdate { get; set; }
        public string? Version { get; init; } = "0.4.0";
        public bool FailCheck { get; init; }
        public bool FailDownload { get; set; }
        public bool FailApply { get; set; }
        public int Checks { get; private set; }
        public int Applies { get; private set; }

        public Task<string?> CheckAsync()
        {
            Checks++;
            return FailCheck ? Task.FromException<string?>(new HttpRequestException("Offline")) : Task.FromResult(Version);
        }

        public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (FailDownload)
                return Task.FromException(new IOException("Interrupted download"));

            HasDownloadedUpdate = true;
            progress.Report(100);
            return Task.CompletedTask;
        }

        public void ApplyAndRestart()
        {
            if (FailApply)
                throw new IOException("Installation failed");
            Applies++;
        }
    }
}
