namespace VideoHarvester.Models;

public class DownloadHistory
{
    public int Id { get; set; }
    public required string SourceUrl { get; set; }
    public string? Title { get; set; }
    public string? LocalPath { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public long? FileSize { get; set; }
    public string? Format { get; set; }
    public string? Quality { get; set; }
    public byte[]? ThumbnailData { get; set; }
    public string? ErrorMessage { get; set; }
}
