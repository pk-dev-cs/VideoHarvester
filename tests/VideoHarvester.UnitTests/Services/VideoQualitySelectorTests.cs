using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.Services;

public class VideoQualitySelectorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Best")]
    [InlineData("invalid")]
    public void GetMaximumHeightReturnsNullForUnboundedQuality(string? quality)
    {
        Assert.Null(VideoQualitySelector.GetMaximumHeight(quality));
    }

    [Theory]
    [InlineData("1080p", 1080)]
    [InlineData("720P", 720)]
    [InlineData(" 480p ", 480)]
    public void GetMaximumHeightParsesConfiguredQuality(string quality, int expectedHeight)
    {
        Assert.Equal(expectedHeight, VideoQualitySelector.GetMaximumHeight(quality));
    }

    [Fact]
    public void GetCdaFormatSupportsSeparateVideoAndAudioStreams()
    {
        Assert.Equal("bestvideo+bestaudio/best", VideoQualitySelector.GetCdaFormat("Best"));
    }

    [Fact]
    public void GetCdaFormatFallsBackWhenPreferredHeightIsUnavailable()
    {
        Assert.Equal(
            "bestvideo[height<=1080]+bestaudio/bestvideo+bestaudio/best",
            VideoQualitySelector.GetCdaFormat("1080p"));
    }

    [Fact]
    public void GetYouTubeFormatUsesConfiguredHeight()
    {
        Assert.Equal(
            "bestvideo[height<=720]+bestaudio/worstvideo+bestaudio/worst",
            VideoQualitySelector.GetYouTubeFormat("720p"));
    }
}
