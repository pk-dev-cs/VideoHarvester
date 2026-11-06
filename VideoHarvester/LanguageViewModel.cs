using CommunityToolkit.Mvvm.ComponentModel;
using VideoHarvester.Localization;
using VideoHarvester.Services;

namespace VideoHarvester;

public partial class LanguageViewModel(ISettingsService settingsService) : ObservableObject
{
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    [ObservableProperty]
    private string selectedLanguage = LocalizationManager.Current.LanguageCode;

    public IReadOnlyList<LanguageOption> Languages { get; } = [new("en", "English"), new("pl", "Polski")];

    partial void OnSelectedLanguageChanged(string value)
    {
        LocalizationManager.Current.SetLanguage(value);
        _ = SaveLanguageAsync();
    }

    private async Task SaveLanguageAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            await settingsService.UpdateLanguageAsync(SelectedLanguage);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(LocalizationManager.Current.Format("Error_SaveLanguage", ex.Message),
                LocalizationManager.Current["Dialog_Error"], System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public sealed record LanguageOption(string Code, string Name);
}
