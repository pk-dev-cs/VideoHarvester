using System.Globalization;

namespace VideoHarvester.Services;

internal static class VideoQualitySelector
{
    public static int? GetMaximumHeight(string? preferredQuality)
    {
        if (string.IsNullOrWhiteSpace(preferredQuality) ||
            preferredQuality.Equals("Best", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string value = preferredQuality.Trim().TrimEnd('p', 'P');
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int height) && height > 0
            ? height
            : null;
    }

    public static string GetYouTubeFormat(string? preferredQuality)
    {
        int? maximumHeight = GetMaximumHeight(preferredQuality);
        return maximumHeight is null
            ? "bestvideo+bestaudio"
            : $"bestvideo[height<={maximumHeight}]+bestaudio/worstvideo+bestaudio/worst";
    }

    public static string GetCdaFormat(string? preferredQuality)
    {
        int? maximumHeight = GetMaximumHeight(preferredQuality);
        return maximumHeight is null
            ? "bestvideo+bestaudio/best"
            : $"bestvideo[height<={maximumHeight}]+bestaudio/bestvideo+bestaudio/best";
    }
}
