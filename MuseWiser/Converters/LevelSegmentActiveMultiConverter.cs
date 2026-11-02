using System.Globalization;
using System.Windows.Data;

namespace MuseWiser.Converters;

/// <summary>
/// 音量レベル（values[0]）とセグメントインデックス（values[1]）から、
/// そのセグメントを点灯させるべきか判定する。16 セグメント構成を前提とする。
/// </summary>
public sealed class LevelSegmentActiveMultiConverter : IMultiValueConverter
{
    public const int SegmentCount = 16;

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [double level, int index])
        {
            return false;
        }

        var threshold = (index + 1) / (double)SegmentCount;
        return level >= threshold;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
