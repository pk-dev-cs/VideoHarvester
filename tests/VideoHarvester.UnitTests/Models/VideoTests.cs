using VideoHarvester.Models;

namespace VideoHarvester.UnitTests.Models;

public class VideoTests
{
    [Theory]
    [InlineData(VideoSource.Wistia, "mp4")]
    [InlineData(VideoSource.YouTube, "mkv")]
    [InlineData(VideoSource.YouTubeWav, "wav")]
    [InlineData(VideoSource.Cda, "mp4")]
    public void FilePathUsesSourceExtension(VideoSource source, string extension)
    {
        var video = CreateVideo(source);
        Assert.Equal(Path.Combine(video.DownloadDirectory, $"{video.FileId}.{extension}"), video.FilePath);
    }

    [Fact]
    public void FilePathIncludesOrderWhenEnabled()
    {
        var video = CreateVideo(VideoSource.YouTube);
        video.Order = 12;
        video.UseOrderNumeration = true;
        Assert.Equal(Path.Combine(video.DownloadDirectory, $"12_{video.FileId}.mkv"), video.FilePath);
    }

    [Theory]
    [InlineData(nameof(Video.Title))]
    [InlineData(nameof(Video.Quality))]
    [InlineData(nameof(Video.Progress))]
    [InlineData(nameof(Video.Status))]
    [InlineData(nameof(Video.ErrorMessage))]
    public void MutableStateRaisesPropertyChanged(string propertyName)
    {
        var video = CreateVideo(VideoSource.Wistia);
        var changedProperties = new List<string?>();
        video.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        switch (propertyName)
        {
            case nameof(Video.Title): video.Title = "Title"; break;
            case nameof(Video.Quality): video.Quality = "720p"; break;
            case nameof(Video.Progress): video.Progress = 50; break;
            case nameof(Video.Status): video.Status = "Downloaded"; break;
            case nameof(Video.ErrorMessage): video.ErrorMessage = "Error"; break;
        }

        Assert.Contains(propertyName, changedProperties);
    }

    private static Video CreateVideo(VideoSource source) => new()
    {
        VideoId = "id",
        Source = source,
        FileId = "file1234",
        DownloadDirectory = Path.Combine(Path.GetTempPath(), "VideoHarvester-tests")
    };
}
