using System.Web;
using HtmlAgilityPack;
using VideoHarvester.Models;

namespace VideoHarvester.Services;

internal static class VideoReferenceParser
{
    public static string? Parse(VideoSource source, string? value) => source switch
    {
        VideoSource.Wistia => ExtractWistiaId(value),
        VideoSource.Cda => ExtractCdaId(value),
        _ => NormalizeYouTubeUrl(value)
    };

    public static string? ExtractWistiaId(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var document = new HtmlAgilityPack.HtmlDocument();
        document.LoadHtml(html);
        var links = document.DocumentNode.SelectNodes("//a[@href]");
        if (links is null)
            return null;

        foreach (var link in links)
        {
            string? href = link.GetAttributeValue("href", null!);
            if (href is null || !href.Contains("wvideo=", StringComparison.Ordinal))
                continue;

            if (Uri.TryCreate(href, UriKind.Absolute, out Uri? uri))
                return HttpUtility.ParseQueryString(uri.Query)["wvideo"];

            string? query = href.Split('?').Skip(1).FirstOrDefault();
            if (query is not null)
                return HttpUtility.ParseQueryString(query)["wvideo"];
        }

        return null;
    }

    public static string? NormalizeYouTubeUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri? uri))
            return null;

        string host = uri.Host.ToLowerInvariant();
        bool valid = host == "youtu.be" ||
                     host == "youtube.com" ||
                     host.EndsWith(".youtube.com", StringComparison.Ordinal) ||
                     host == "youtube-nocookie.com" ||
                     host.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal);
        return valid ? uri.AbsoluteUri : null;
    }

    public static string? ExtractCdaId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string trimmedValue = value.Trim();
        if (Uri.TryCreate(trimmedValue, UriKind.Absolute, out Uri? uri))
        {
            string host = uri.Host.ToLowerInvariant();
            if (host == "cda.pl" || host.EndsWith(".cda.pl", StringComparison.Ordinal))
            {
                var match = System.Text.RegularExpressions.Regex.Match(uri.AbsolutePath, @"/video/([a-zA-Z0-9]+)");
                return match.Success ? match.Groups[1].Value : null;
            }

            return null;
        }

        var idMatch = System.Text.RegularExpressions.Regex.Match(trimmedValue, @"^([a-zA-Z0-9]+)$");
        return idMatch.Success ? idMatch.Groups[1].Value : null;
    }
}
