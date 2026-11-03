using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MuseWiser.Converters;

/// <summary>
/// スペクトラムバンドのピークホールド位置（values[0]）とセグメントインデックス（values[1]）から、
/// ピークホールドを表示すべきセグメントだけ発光色（現在のレベル色と同系統）を返すオーバーレイ用コンバータ。
/// 該当セグメント以外は透明を返し、通常の点灯表示（<see cref="SpectrumSegmentBrushMultiConverter"/>）に
/// 重ねて描画することでピークホールドの表示を実現する。
/// </summary>
public sealed class SpectrumPeakSegmentBrushMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [int peakSegmentIndex, int segmentIndex] || segmentIndex != peakSegmentIndex)
        {
            return Brushes.Transparent;
        }

        var position = (segmentIndex + 1) / (double)ViewModels.SpectrumBandViewModel.SegmentCount;
        return LevelColorPalette.ResolveColor(position);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
