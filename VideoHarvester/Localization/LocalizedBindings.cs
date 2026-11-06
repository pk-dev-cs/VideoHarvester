using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Binding = System.Windows.Data.Binding;

namespace VideoHarvester.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class TextExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new Binding($"[{key}]")
    {
        Source = LocalizationManager.Current,
        Mode = BindingMode.OneWay
    }.ProvideValue(serviceProvider);
}

public abstract class LocalizedBindingExtension : MarkupExtension
{
    public BindingBase Value { get; set; } = new Binding();
    protected abstract IMultiValueConverter CreateConverter();

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new MultiBinding { Converter = CreateConverter(), Mode = BindingMode.OneWay };
        binding.Bindings.Add(Value);
        binding.Bindings.Add(new Binding(nameof(LocalizationManager.Culture)) { Source = LocalizationManager.Current });
        return binding.ProvideValue(serviceProvider);
    }
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class FormatExtension : LocalizedBindingExtension
{
    public string Key { get; set; } = string.Empty;
    protected override IMultiValueConverter CreateConverter() => new LocalizedConverter(Key, true);
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class ValueExtension : LocalizedBindingExtension
{
    public string Prefix { get; set; } = string.Empty;
    protected override IMultiValueConverter CreateConverter() => new LocalizedConverter(Prefix, false);
}

internal sealed class LocalizedConverter(string key, bool format) : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] == DependencyProperty.UnsetValue)
            return string.Empty;

        var texts = LocalizationManager.Current;
        if (!format)
            return texts.TranslateValue(key, values[0]);
        if (key == "Queue_ItemCount" && values[0] is int count)
            return texts.FormatCount(count);
        return texts.Format(key, values[0]);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
