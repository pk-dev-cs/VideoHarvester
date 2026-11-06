using Microsoft.Data.Sqlite;
using System.IO;

namespace VideoHarvester.Services;

internal static class HistoryDatabaseMigration
{
    public static async Task MigrateAsync(string legacyPath, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        if (File.Exists(destinationPath) || !File.Exists(legacyPath))
            return;

        string temporaryPath = destinationPath + ".migrating-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = legacyPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temporaryPath, Pooling = false }.ToString()))
            {
                await source.OpenAsync();
                await destination.OpenAsync();
                source.BackupDatabase(destination);
            }

            try
            {
                File.Move(temporaryPath, destinationPath);
            }
            catch (IOException) when (File.Exists(destinationPath))
            {
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
