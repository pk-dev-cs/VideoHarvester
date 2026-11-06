using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace VideoHarvester.Localization;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private readonly ResourceManager _resources = new("VideoHarvester.Resources.Strings", typeof(LocalizationManager).Assembly);
    private CultureInfo _culture = CultureInfo.GetCultureInfo("en-US");

    public static LocalizationManager Current { get; } = new();
    public CultureInfo Culture => _culture;
    public string LanguageCode => _culture.TwoLetterISOLanguageName;
    public string this[string key] => _resources.GetString(key, _culture) ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetLanguage(string? language)
    {
        string code = language == "pl" ? "pl" : "en";
        bool changed = LanguageCode != code;
        _culture = CultureInfo.GetCultureInfo(code == "pl" ? "pl-PL" : "en-US");
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _culture;
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _culture;
        if (!changed)
            return;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageCode)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public string Format(string key, params object?[] arguments) => string.Format(_culture, this[key], arguments);

    public string TranslateValue(string prefix, object? value)
    {
        string text = value?.ToString() ?? string.Empty;
        return _resources.GetString($"{prefix}_{text.Replace(" ", string.Empty)}", _culture) ?? text;
    }

    public string FormatCount(int count)
    {
        string form = count == 1 ? "One" : LanguageCode == "pl" && count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14) ? "Few" : "Many";
        return Format($"Queue_ItemCount{form}", count);
    }
}
