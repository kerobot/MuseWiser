using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MuseWiser.ViewModels;

namespace MuseWiser.Converters;

/// <summary>
/// スペクトラムバンドの点灯ブロック数（values[0]）とセグメントインデックス（values[1]）から、
/// <see cref="LevelColorPalette"/> を用いた LED 風の色（緑→黄→赤）を返す。消灯時は暗いグレーを返す。
/// 音量メーター用の <see cref="LevelSegmentBrushMultiConverter"/> とは異なり、
/// 量子化済みの整数値（ActiveSegmentCount）を前提とする。
/// </summary>
public sealed class SpectrumSegmentBrushMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [int activeSegmentCount, int segmentIndex] || segmentIndex >= activeSegmentCount)
        {
            return LevelColorPalette.OffBrush;
        }

        var position = (segmentIndex + 1) / (double)SpectrumBandViewModel.SegmentCount;
        return LevelColorPalette.ResolveColor(position);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
