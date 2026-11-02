using System.Globalization;
using System.Windows.Data;

namespace MuseWiser.Converters;

/// <summary>
/// スペクトラムバンドのレベル（0.0～1.0）を、指定した最大高さに応じたバーの高さへ変換する。
/// ConverterParameter に最大高さ（double）を指定する。
/// </summary>
public sealed class SpectrumBarHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double level)
        {
            return 0.0;
        }

        var maxHeight = parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 40.0;

        return Math.Clamp(level, 0.0, 1.0) * maxHeight;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
