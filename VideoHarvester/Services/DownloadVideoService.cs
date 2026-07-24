using System.IO;
using System.Windows.Media.Imaging;
using VideoHarvester.Models;

namespace VideoHarvester.Services;

public interface IDownloadVideoService
{
    Task DownloadVideo(Video video, CancellationToken cancellationToken = default);
}

public class DownloadVideoService : IDownloadVideoService
{
    private readonly IDownloadHistoryService _historyService;

    public DownloadVideoService(IDownloadHistoryService historyService)
    {
        _historyService = historyService;
    }

    public async Task DownloadVideo(Video video, CancellationToken cancellationToken = default)
    {
        // Convert thumbnail to bytes if available
        byte[]? thumbnailBytes = null;
        if (video.ThumbnailImage != null)
        {
            thumbnailBytes = BitmapImageToBytes(video.ThumbnailImage);
        }

        // Create history record
        var history = new DownloadHistory
        {
            SourceUrl = video.VideoId,
            Status = "Started",
            CreatedAt = DateTime.Now,
            ThumbnailData = thumbnailBytes,
            Format = video.Source switch
            {
                VideoSource.YouTube => "mkv",
                VideoSource.YouTubeWav => "wav",
                _ => "mp4"
            }
        };

        var historyId = await _historyService.AddDownloadAsync(history);
        history.Id = historyId;

        try
        {
            // Check if already cancelled
            cancellationToken.ThrowIfCancellationRequested();

            // Download the video
            await (video.Source switch
            {
                VideoSource.YouTube => YouTubeDownloader.Download(video, cancellationToken),
                VideoSource.YouTubeWav => YouTubeWavDownloader.Download(video, cancellationToken),
                VideoSource.Wistia => WistiaDownloader.Download(video, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(video.Source), video.Source, "Unsupported video source.")
            });

            // Update history on success
            history.Status = video.Status;
            history.Title = video.Title;
            history.Quality = video.Quality;
            history.LocalPath = video.FilePath;
            history.FinishedAt = DateTime.Now;
            history.ErrorMessage = video.ErrorMessage;

            // Get file size if exists
            if (File.Exists(video.FilePath))
            {
                var fileInfo = new FileInfo(video.FilePath);
                history.FileSize = fileInfo.Length;
            }

            // Update thumbnail if it was generated during download
            if (video.ThumbnailImage != null)
            {
                history.ThumbnailData = BitmapImageToBytes(video.ThumbnailImage);
            }

            await _historyService.UpdateDownloadAsync(history);
        }
        catch (OperationCanceledException)
        {
            // Update history on cancellation
            history.Status = "Cancelled";
            history.FinishedAt = DateTime.Now;
            await _historyService.UpdateDownloadAsync(history);
            throw;
        }
        catch (Exception ex)
        {
            // Update history on failure
            history.Status = "Failed";
            history.ErrorMessage = ex.Message;
            history.FinishedAt = DateTime.Now;
            await _historyService.UpdateDownloadAsync(history);
            throw;
        }
    }

    private static byte[]? BitmapImageToBytes(BitmapImage? image)
    {
        if (image == null) return null;

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
