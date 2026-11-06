using System.Collections;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using VideoHarvester;
using VideoHarvester.Localization;
using VideoHarvester.Services;

internal static class LocalizationChecks
{
    public static async Task RunAsync()
    {
        var resources = new ResourceManager("VideoHarvester.Resources.Strings", typeof(LocalizationManager).Assembly);
        var english = resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
        var polish = resources.GetResourceSet(CultureInfo.GetCultureInfo("pl"), true, false)!;
        var englishKeys = english.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).ToHashSet();
        Require(polish is not null && englishKeys.SetEquals(polish.Cast<DictionaryEntry>().Select(entry => (string)entry.Key)), "Both languages must contain the same resource keys.");
        foreach (string key in englishKeys)
        {
            string en = (string)english.GetObject(key)!;
            string pl = (string)polish!.GetObject(key)!;
            Require(!string.IsNullOrWhiteSpace(en) && !string.IsNullOrWhiteSpace(pl), $"Empty resource: {key}");
            var enFormat = System.Text.CompositeFormat.Parse(en);
            var plFormat = System.Text.CompositeFormat.Parse(pl);
            Require(enFormat.MinimumArgumentCount == plFormat.MinimumArgumentCount, $"Inconsistent placeholders: {key}");
        }

        var texts = LocalizationManager.Current;
        texts.SetLanguage("pl");
        Require(texts["Menu_Settings"] == "Ustawienia", "Polish satellite resources must load.");
        foreach (var (count, expected) in new[] { (0, "0 pozycji"), (1, "1 pozycja"), (2, "2 pozycje"), (5, "5 pozycji"), (12, "12 pozycji"), (22, "22 pozycje"), (112, "112 pozycji") })
            Require(texts.FormatCount(count) == expected, "Polish plural rules must match the queue count.");
        Require(texts.TranslateValue("Status", "Paused") == "Wstrzymano", "Internal statuses must have translated labels.");
        Require(texts.TranslateValue("Quality", "1080p") == "1080p", "Untranslated technical values must remain intact.");
        using (var updates = new AppUpdatesViewModel(new CurrentUpdates(), () => true))
        {
            await updates.CheckForUpdatesCommand.ExecuteAsync(null);
            Require(updates.Status == "Masz aktualną wersję." && updates.DismissButtonText == "Zamknij", "Current-version state must use Close in Polish.");
            texts.SetLanguage("en");
            Require(updates.Status == "You're up to date." && updates.DismissButtonText == "Close", "An existing update state must translate immediately.");
            updates.AvailableVersion = "0.4.0";
            Require(updates.DismissButtonText == "Later", "A deferred update must use Later.");
        }

        string directory = Path.Combine(Path.GetTempPath(), "VideoHarvester-localization-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "settings.json");
            var service = new SettingsService(path);
            var settings = await service.LoadSettingsAsync();
            settings.DefaultDownloadPath = "custom-downloads";
            settings.MaxConcurrentDownloads = 3;
            await service.SaveSettingsAsync(settings);
            await service.UpdateLanguageAsync("pl");
            settings.PreferredVideoQuality = "720p";
            await service.SaveSettingsAsync(settings);
            var restored = await new SettingsService(path).LoadSettingsAsync();
            Require(restored.Language == "pl" && restored.DefaultDownloadPath == "custom-downloads" && restored.MaxConcurrentDownloads == 3 && restored.PreferredVideoQuality == "720p", "Language persistence must preserve other settings and survive saving an older settings object.");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
            texts.SetLanguage("en");
        }
        Console.WriteLine("PASS localization-resources-plurals-live-status-and-persistence");
    }

    public static void RunBindings()
    {
        var texts = LocalizationManager.Current;
        var label = new TextBlock();
        label.SetBinding(TextBlock.TextProperty, (BindingBase)new TextExtension("Menu_Settings").ProvideValue(null!));
        var status = new TextBlock();
        status.SetBinding(TextBlock.TextProperty, (BindingBase)new ValueExtension { Prefix = "Status", Value = new Binding { Source = "Paused" } }.ProvideValue(null!));
        var count = new TextBlock();
        count.SetBinding(TextBlock.TextProperty, (BindingBase)new FormatExtension { Key = "Queue_ItemCount", Value = new Binding { Source = 22 } }.ProvideValue(null!));
        foreach (string language in new[] { "pl", "en", "pl", "en" })
        {
            texts.SetLanguage(language);
            Require(label.Text == texts["Menu_Settings"] && status.Text == texts.TranslateValue("Status", "Paused") && count.Text == texts.FormatCount(22), "Existing WPF bindings must update when the language changes.");
        }
        Console.WriteLine("PASS localization-live-wpf-bindings");
        var styles = new ResourceDictionary { Source = new Uri("pack://application:,,,/VideoHarvester;component/Styles.xaml") };
        var selector = new ComboBox
        {
            Style = (Style)styles[typeof(ComboBox)],
            Width = 280,
            Height = 42,
            ItemTemplate = (DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding Name}' /></DataTemplate>"),
            ItemsSource = new[] { new LanguageViewModel.LanguageOption("en", "English"), new LanguageViewModel.LanguageOption("pl", "Polski") }
        };
        foreach (int index in new[] { 0, 1 })
        {
            selector.SelectedIndex = index;
            selector.Measure(new Size(280, 42));
            selector.Arrange(new Rect(0, 0, 280, 42));
            selector.UpdateLayout();
            string expected = index == 0 ? "English" : "Polski";
            Require(Descendants(selector).OfType<TextBlock>().Any(block => block.Text == expected), "The closed language selector must display the language name using its item template.");
        }
        Console.WriteLine("PASS language-selector-display");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class CurrentUpdates : IAppUpdateService
    {
        public bool IsInstalled => true;
        public bool HasDownloadedUpdate => false;
        public Task<string?> CheckAsync() => Task.FromResult<string?>(null);
        public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void ApplyAndRestart() => throw new NotSupportedException();
    }
}
