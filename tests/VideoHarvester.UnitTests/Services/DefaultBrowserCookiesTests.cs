using System.Diagnostics;
using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.Services;

public class DefaultBrowserCookiesTests
{
    [Fact]
    public void AddToDoesNothingWithoutBrowser()
    {
        var startInfo = new ProcessStartInfo();
        DefaultBrowserCookies.AddTo(startInfo, null);
        Assert.Empty(startInfo.ArgumentList);
    }

    [Fact]
    public void AddToAddsBrowserCookieArguments()
    {
        var startInfo = new ProcessStartInfo();
        DefaultBrowserCookies.AddTo(startInfo, "firefox");
        Assert.Equal(["--cookies-from-browser", "firefox"], startInfo.ArgumentList);
    }
}
