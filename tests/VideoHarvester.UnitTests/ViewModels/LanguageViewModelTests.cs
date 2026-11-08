using VideoHarvester.Localization;
using VideoHarvester.Models;
using VideoHarvester.Services;

namespace VideoHarvester.UnitTests.ViewModels;

public class LanguageViewModelTests : IDisposable
{
    [Fact]
    public void LanguagesExposeEnglishAndPolish()
    {
        var viewModel = new LanguageViewModel(new RecordingSettingsService());
        Assert.Collection(
            viewModel.Languages,
            language => Assert.Equal(("en", "English"), (language.Code, language.Name)),
            language => Assert.Equal(("pl", "Polski"), (language.Code, language.Name)));
    }

    [Fact]
    public async Task ChangingLanguageUpdatesLocalizationAndPersistsSelection()
    {
        var service = new RecordingSettingsService();
        var viewModel = new LanguageViewModel(service);

        viewModel.SelectedLanguage = "pl";
        await service.LanguageSaved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("pl", LocalizationManager.Current.LanguageCode);
        Assert.Equal("pl", service.Language);
    }

    public void Dispose() => LocalizationManager.Current.SetLanguage("en");

    private sealed class RecordingSettingsService : ISettingsService
    {
        public TaskCompletionSource LanguageSaved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Language { get; private set; }
        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(new AppSettings());
        public Task SaveSettingsAsync(AppSettings settings) => Task.CompletedTask;
        public Task UpdateLanguageAsync(string language)
        {
            Language = language;
            LanguageSaved.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
