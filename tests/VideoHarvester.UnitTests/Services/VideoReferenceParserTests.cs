using VideoHarvester.Models;
using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.Services;

public class VideoReferenceParserTests
{
    [Theory]
    [InlineData("https://youtu.be/abc123", "https://youtu.be/abc123")]
    [InlineData("https://www.youtube.com/watch?v=abc123", "https://www.youtube.com/watch?v=abc123")]
    [InlineData("https://youtube-nocookie.com/embed/abc123", "https://youtube-nocookie.com/embed/abc123")]
    public void NormalizeYouTubeUrlAcceptsSupportedHosts(string value, string expected)
    {
        Assert.Equal(expected, VideoReferenceParser.NormalizeYouTubeUrl(value));
    }

    [Theory]
    [InlineData("https://notyoutube.com/watch?v=abc123")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void NormalizeYouTubeUrlRejectsUnsupportedValues(string? value)
    {
        Assert.Null(VideoReferenceParser.NormalizeYouTubeUrl(value));
    }

    [Theory]
    [InlineData("https://www.cda.pl/video/2882914921", "2882914921")]
    [InlineData("https://ebd.cda.pl/video/3383563ba", "3383563ba")]
    [InlineData("3383563ba", "3383563ba")]
    public void ExtractCdaIdReturnsIdentifier(string value, string expected)
    {
        Assert.Equal(expected, VideoReferenceParser.ExtractCdaId(value));
    }

    [Theory]
    [InlineData("https://example.com/video/3383563ba")]
    [InlineData("https://cda.pl/not-video/3383563ba")]
    [InlineData("invalid-id!")]
    public void ExtractCdaIdRejectsInvalidValues(string value)
    {
        Assert.Null(VideoReferenceParser.ExtractCdaId(value));
    }

    [Fact]
    public void ExtractWistiaIdReadsAbsoluteLink()
    {
        const string html = "<a href=\"https://example.com/lesson?wvideo=abc123\">Video</a>";
        Assert.Equal("abc123", VideoReferenceParser.ExtractWistiaId(html));
    }

    [Fact]
    public void ExtractWistiaIdReadsRelativeLink()
    {
        const string html = "<a href=\"/lesson?wvideo=relative123\">Video</a>";
        Assert.Equal("relative123", VideoReferenceParser.ExtractWistiaId(html));
    }

    [Fact]
    public void ParseDelegatesBySource()
    {
        Assert.Equal("2882914921", VideoReferenceParser.Parse(VideoSource.Cda, "https://cda.pl/video/2882914921"));
        Assert.Equal("https://youtu.be/abc", VideoReferenceParser.Parse(VideoSource.YouTubeWav, "https://youtu.be/abc"));
    }
}
