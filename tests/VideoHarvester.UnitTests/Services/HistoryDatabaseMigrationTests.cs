using Microsoft.Data.Sqlite;
using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.Services;

public class HistoryDatabaseMigrationTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "VideoHarvester-migration-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MigrateCopiesAConsistentSqliteDatabase()
    {
        string legacyPath = Path.Combine(_folder, "legacy", "downloads.db");
        string destinationPath = Path.Combine(_folder, "current", "downloads.db");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        await CreateDatabase(legacyPath, "legacy-value");

        await HistoryDatabaseMigration.MigrateAsync(legacyPath, destinationPath);

        Assert.True(File.Exists(destinationPath));
        Assert.Equal("legacy-value", await ReadValue(destinationPath));
        Assert.True(File.Exists(legacyPath));
    }

    [Fact]
    public async Task MigrateDoesNotOverwriteExistingDestination()
    {
        string legacyPath = Path.Combine(_folder, "legacy.db");
        string destinationPath = Path.Combine(_folder, "destination", "downloads.db");
        await CreateDatabase(legacyPath, "legacy-value");
        await CreateDatabase(destinationPath, "current-value");

        await HistoryDatabaseMigration.MigrateAsync(legacyPath, destinationPath);

        Assert.Equal("current-value", await ReadValue(destinationPath));
    }

    [Fact]
    public async Task MigrateWithoutLegacyFileOnlyCreatesDestinationDirectory()
    {
        string destinationPath = Path.Combine(_folder, "current", "downloads.db");
        await HistoryDatabaseMigration.MigrateAsync(Path.Combine(_folder, "missing.db"), destinationPath);
        Assert.True(Directory.Exists(Path.GetDirectoryName(destinationPath)));
        Assert.False(File.Exists(destinationPath));
    }

    private static async Task CreateDatabase(string path, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE sample (value TEXT); INSERT INTO sample VALUES ($value)";
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ReadValue(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM sample";
        return (string)(await command.ExecuteScalarAsync())!;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }
}
