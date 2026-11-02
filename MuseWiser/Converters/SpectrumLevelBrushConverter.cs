using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MuseWiser.Converters;

/// <summary>
/// スペクトラムバンドのレベル（0.0～1.0）から、PlayMuse 風の LED カラー（緑→黄→赤）を返す。
/// </summary>
public sealed class SpectrumLevelBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = CreateFrozenBrush(0x00, 0xE6, 0x76);
    private static readonly SolidColorBrush YellowBrush = CreateFrozenBrush(0xFF, 0xD1, 0x00);
    private static readonly SolidColorBrush RedBrush = CreateFrozenBrush(0xFF, 0x3B, 0x30);

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value is double d ? d : 0.0;

        return level switch
        {
            <= 0.70 => GreenBrush,
            <= 0.90 => YellowBrush,
            _ => RedBrush,
        };
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
