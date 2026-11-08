using System.Globalization;
using System.Windows;
using System.Windows.Media;
using VideoHarvester.Converters;

namespace VideoHarvester.UnitTests.Converters;

public class ConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData(true, null, Visibility.Visible)]
    [InlineData(false, null, Visibility.Collapsed)]
    [InlineData(true, "Invert", Visibility.Collapsed)]
    [InlineData(false, "Invert", Visibility.Visible)]
    public void BoolToVisibilityConvertsAndInverts(bool value, string? parameter, Visibility expected)
    {
        Assert.Equal(expected, new BoolToVisibilityConverter().Convert(value, typeof(Visibility), parameter!, Culture));
    }

    [Theory]
    [InlineData("Downloaded", Visibility.Visible)]
    [InlineData("Failed", Visibility.Collapsed)]
    [InlineData(null, Visibility.Collapsed)]
    public void ButtonVisibilityDependsOnDownloadedStatus(string? status, Visibility expected)
    {
        Assert.Equal(expected, new ButtonVisibilityConverter().Convert(status!, typeof(Visibility), null!, Culture));
    }

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1572864L, "1.5 MB")]
    [InlineData(1073741824L, "1 GB")]
    public void FileSizeUsesReadableUnits(long bytes, string expected)
    {
        Assert.Equal(expected, new FileSizeConverter().Convert(bytes, typeof(string), null, Culture));
    }

    [Fact]
    public void WidthAdjustmentSubtractsLayoutMargin()
    {
        Assert.Equal(160d, new WidthAdjustmentConverter().Convert(200d, typeof(double), null!, Culture));
    }

    [Fact]
    public void StatusColorUsesGreenForDownloaded()
    {
        Assert.Same(Brushes.Green, new StatusColorConverter().Convert("Downloaded", typeof(Brush), null!, Culture));
        Assert.Same(Brushes.Yellow, new StatusColorConverter().Convert("Failed", typeof(Brush), null!, Culture));
    }

    [Theory]
    [InlineData(true, 34, 197, 94)]
    [InlineData(false, 239, 68, 68)]
    public void DependencyColorReflectsAvailability(bool available, byte red, byte green, byte blue)
    {
        var brush = Assert.IsType<SolidColorBrush>(new DependencyStatusColorConverter().Convert(available, typeof(Brush), null!, Culture));
        Assert.Equal(Color.FromRgb(red, green, blue), brush.Color);
    }
}
