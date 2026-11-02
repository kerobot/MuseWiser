using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MuseWiser.Converters;

/// <summary>
/// bool 値を <see cref="Visibility"/> に変換する。true = Visible, false = Collapsed。
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}
