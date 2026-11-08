using VideoHarvester.Localization;

namespace VideoHarvester.UnitTests.Localization;

public class LocalizationManagerTests : IDisposable
{
    [Theory]
    [InlineData("pl", "pl")]
    [InlineData("en", "en")]
    [InlineData("unsupported", "en")]
    [InlineData(null, "en")]
    public void SetLanguageNormalizesLanguageCode(string? requested, string expected)
    {
        LocalizationManager.Current.SetLanguage(requested);
        Assert.Equal(expected, LocalizationManager.Current.LanguageCode);
    }

    [Fact]
    public void SetLanguageRaisesAllBindingNotifications()
    {
        LocalizationManager.Current.SetLanguage("en");
        var properties = new List<string?>();
        LocalizationManager.Current.PropertyChanged += Handler;
        try
        {
            LocalizationManager.Current.SetLanguage("pl");
        }
        finally
        {
            LocalizationManager.Current.PropertyChanged -= Handler;
        }

        Assert.Contains(nameof(LocalizationManager.Culture), properties);
        Assert.Contains(nameof(LocalizationManager.LanguageCode), properties);
        Assert.Contains("Item[]", properties);

        void Handler(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => properties.Add(args.PropertyName);
    }

    [Theory]
    [InlineData(1, "1 pozycja")]
    [InlineData(2, "2 pozycje")]
    [InlineData(5, "5 pozycji")]
    [InlineData(12, "12 pozycji")]
    [InlineData(22, "22 pozycje")]
    public void FormatCountUsesPolishPluralForms(int count, string expected)
    {
        LocalizationManager.Current.SetLanguage("pl");
        Assert.Equal(expected, LocalizationManager.Current.FormatCount(count));
    }

    [Fact]
    public void MissingResourceReturnsKey()
    {
        Assert.Equal("Missing_Key", LocalizationManager.Current["Missing_Key"]);
    }

    public void Dispose() => LocalizationManager.Current.SetLanguage("en");
}
