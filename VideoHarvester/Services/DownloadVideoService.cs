using VideoHarvester.Models;

namespace VideoHarvester.Services;

public interface IDownloadVideoService
{
    Task DownloadVideo(Video video);
}

public class DownloadVideoService : IDownloadVideoService
{
    public Task DownloadVideo(Video video) => video.Source switch
    {
        VideoSource.YouTube => YouTubeDownloader.Download(video),
        VideoSource.YouTubeWav => YouTubeWavDownloader.Download(video),
        VideoSource.Wistia => WistiaDownloader.Download(video),
        _ => throw new ArgumentOutOfRangeException(nameof(video.Source), video.Source, "Unsupported video source.")
    };
}
