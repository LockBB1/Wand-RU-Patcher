using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WandRuInstaller.Converters;

/// <summary>
/// Visible, если имя enum-значения входит в ConverterParameter (список через запятую), иначе Collapsed.
/// Пример: Visibility="{Binding State, Converter={StaticResource EnumToVisibility}, ConverterParameter=Ready,Patched}"
/// </summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string names) return Visibility.Collapsed;
        var current = value.ToString();
        foreach (var name in names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (string.Equals(name, current, StringComparison.Ordinal))
                return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
