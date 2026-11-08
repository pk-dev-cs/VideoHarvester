using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.Services;

public class ToolchainLocatorTests : IDisposable
{
    private readonly string _toolsDirectory = Path.Combine(AppContext.BaseDirectory, "tools");

    [Fact]
    public void BundledExecutablesTakePrecedence()
    {
        Directory.CreateDirectory(_toolsDirectory);
        foreach (string fileName in new[] { "yt-dlp.exe", "node.exe", "ffmpeg.exe", "ffprobe.exe" })
            File.WriteAllBytes(Path.Combine(_toolsDirectory, fileName), []);

        Assert.Equal(Path.Combine(_toolsDirectory, "yt-dlp.exe"), ToolchainLocator.FindYtDlp());
        Assert.Equal(Path.Combine(_toolsDirectory, "node.exe"), ToolchainLocator.FindNode());
        Assert.Equal(Path.Combine(_toolsDirectory, "ffmpeg.exe"), ToolchainLocator.FindFFmpeg());
        Assert.Equal(Path.Combine(_toolsDirectory, "ffprobe.exe"), ToolchainLocator.FindFFprobe());
    }

    public void Dispose()
    {
        if (Directory.Exists(_toolsDirectory))
            Directory.Delete(_toolsDirectory, true);
    }
}
