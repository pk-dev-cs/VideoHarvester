using Microsoft.Data.Sqlite;
using System.IO;
using VideoHarvester.Models;

namespace VideoHarvester.Services;

public interface IDownloadHistoryService
{
    Task InitializeDatabaseAsync();
    Task<int> AddDownloadAsync(DownloadHistory download);
    Task UpdateDownloadAsync(DownloadHistory download);
    Task<DownloadHistory?> GetDownloadByIdAsync(int id);
    Task<List<DownloadHistory>> GetAllDownloadsAsync();
    Task<List<DownloadHistory>> GetDownloadsByStatusAsync(string status);
    Task DeleteDownloadAsync(int id);
}

public class DownloadHistoryService : IDownloadHistoryService
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public DownloadHistoryService()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _databasePath = Path.Combine(appDirectory, "downloads.db");
        _connectionString = $"Data Source={_databasePath}";
    }

    public async Task InitializeDatabaseAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS downloads (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_url TEXT NOT NULL,
                title TEXT,
                local_path TEXT,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL,
                finished_at TEXT,
                file_size INTEGER,
                format TEXT,
                quality TEXT,
                thumbnail_data BLOB,
                error_message TEXT
            )";

        await createTableCommand.ExecuteNonQueryAsync();

        // Migration: Add thumbnail_data column if it doesn't exist
        var checkColumnCommand = connection.CreateCommand();
        checkColumnCommand.CommandText = "PRAGMA table_info(downloads)";

        bool hasThumbnailDataColumn = false;
        using (var reader = await checkColumnCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                string columnName = reader.GetString(1);
                if (columnName == "thumbnail_data")
                {
                    hasThumbnailDataColumn = true;
                    break;
                }
            }
        }

        if (!hasThumbnailDataColumn)
        {
            var alterTableCommand = connection.CreateCommand();
            alterTableCommand.CommandText = "ALTER TABLE downloads ADD COLUMN thumbnail_data BLOB";
            await alterTableCommand.ExecuteNonQueryAsync();
        }
    }

    public async Task<int> AddDownloadAsync(DownloadHistory download)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO downloads (source_url, title, local_path, status, created_at, finished_at, file_size, format, quality, thumbnail_data, error_message)
            VALUES ($source_url, $title, $local_path, $status, $created_at, $finished_at, $file_size, $format, $quality, $thumbnail_data, $error_message);
            SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("$source_url", download.SourceUrl);
        command.Parameters.AddWithValue("$title", (object?)download.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$local_path", (object?)download.LocalPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", download.Status);
        command.Parameters.AddWithValue("$created_at", download.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("$finished_at", download.FinishedAt?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$file_size", (object?)download.FileSize ?? DBNull.Value);
        command.Parameters.AddWithValue("$format", (object?)download.Format ?? DBNull.Value);
        command.Parameters.AddWithValue("$quality", (object?)download.Quality ?? DBNull.Value);
        command.Parameters.AddWithValue("$thumbnail_data", (object?)download.ThumbnailData ?? DBNull.Value);
        command.Parameters.AddWithValue("$error_message", (object?)download.ErrorMessage ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync();
        var id = Convert.ToInt32(result);
        return id;
    }

    public async Task UpdateDownloadAsync(DownloadHistory download)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE downloads 
            SET source_url = $source_url,
                title = $title,
                local_path = $local_path,
                status = $status,
                created_at = $created_at,
                finished_at = $finished_at,
                file_size = $file_size,
                format = $format,
                quality = $quality,
                thumbnail_data = $thumbnail_data,
                error_message = $error_message
            WHERE id = $id";

        command.Parameters.AddWithValue("$id", download.Id);
        command.Parameters.AddWithValue("$source_url", download.SourceUrl);
        command.Parameters.AddWithValue("$title", (object?)download.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$local_path", (object?)download.LocalPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", download.Status);
        command.Parameters.AddWithValue("$created_at", download.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("$finished_at", download.FinishedAt?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$file_size", (object?)download.FileSize ?? DBNull.Value);
        command.Parameters.AddWithValue("$format", (object?)download.Format ?? DBNull.Value);
        command.Parameters.AddWithValue("$quality", (object?)download.Quality ?? DBNull.Value);
        command.Parameters.AddWithValue("$thumbnail_data", (object?)download.ThumbnailData ?? DBNull.Value);
        command.Parameters.AddWithValue("$error_message", (object?)download.ErrorMessage ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<DownloadHistory?> GetDownloadByIdAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, source_url, title, local_path, status, created_at, 
                                       finished_at, file_size, format, quality, thumbnail_data, error_message 
                                FROM downloads WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadDownloadFromReader(reader);
        }

        return null;
    }

    public async Task<List<DownloadHistory>> GetAllDownloadsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, source_url, title, local_path, status, created_at, 
                                       finished_at, file_size, format, quality, thumbnail_data, error_message 
                                FROM downloads ORDER BY created_at DESC";

        var downloads = new List<DownloadHistory>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            downloads.Add(ReadDownloadFromReader(reader));
        }

        return downloads;
    }

    public async Task<List<DownloadHistory>> GetDownloadsByStatusAsync(string status)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, source_url, title, local_path, status, created_at, 
                                       finished_at, file_size, format, quality, thumbnail_data, error_message 
                                FROM downloads WHERE status = $status ORDER BY created_at DESC";
        command.Parameters.AddWithValue("$status", status);

        var downloads = new List<DownloadHistory>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            downloads.Add(ReadDownloadFromReader(reader));
        }

        return downloads;
    }

    public async Task DeleteDownloadAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM downloads WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);

        await command.ExecuteNonQueryAsync();
    }

    private static DownloadHistory ReadDownloadFromReader(SqliteDataReader reader)
    {
        return new DownloadHistory
        {
            Id = reader.GetInt32(0),
            SourceUrl = reader.GetString(1),
            Title = reader.IsDBNull(2) ? null : reader.GetString(2),
            LocalPath = reader.IsDBNull(3) ? null : reader.GetString(3),
            Status = reader.GetString(4),
            CreatedAt = DateTime.Parse(reader.GetString(5)),
            FinishedAt = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
            FileSize = reader.IsDBNull(7) ? null : reader.GetInt64(7),
            Format = reader.IsDBNull(8) ? null : reader.GetString(8),
            Quality = reader.IsDBNull(9) ? null : reader.GetString(9),
            ThumbnailData = reader.IsDBNull(10) ? null : (byte[])reader.GetValue(10),
            ErrorMessage = reader.IsDBNull(11) ? null : reader.GetString(11)
        };
    }
}
