using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MuseWiser.Converters;

/// <summary>
/// 緑→黄→赤の LED 風配色ロジックを提供する共通ヘルパー。
/// <see cref="LevelSegmentBrushMultiConverter"/> とスペクトラム用コンバータの両方から参照される。
/// </summary>
internal static class LevelColorPalette
{
    public static readonly SolidColorBrush OffBrush = CreateFrozenBrush(0x33, 0x33, 0x33);
    public static readonly SolidColorBrush GreenBrush = CreateFrozenBrush(0x00, 0xE6, 0x76);
    public static readonly SolidColorBrush YellowBrush = CreateFrozenBrush(0xFF, 0xD1, 0x00);
    public static readonly SolidColorBrush RedBrush = CreateFrozenBrush(0xFF, 0x3B, 0x30);

    /// <summary>
    /// セグメントの位置（0.0〜1.0、1.0 が最上段）に応じた発光色を返す
    /// （PlayMuse 風：下位は緑、中位は黄、上位は赤）。
    /// </summary>
    public static SolidColorBrush ResolveColor(double position) => position switch
    {
        <= 0.70 => GreenBrush,
        <= 0.90 => YellowBrush,
        _ => RedBrush,
    };

    public static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// 音量レベル（values[0]）とセグメントインデックス（values[1]）から、
/// PlayMuse のレベルメーターを参考にした LED 風の色（緑→黄→赤）を返す。
/// 消灯時は暗いグレーを返す。
/// </summary>
public sealed class LevelSegmentBrushMultiConverter : IMultiValueConverter
{
    private const int SegmentCount = LevelSegmentActiveMultiConverter.SegmentCount;

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [double level, int index])
        {
            return LevelColorPalette.OffBrush;
        }

        var position = (index + 1) / (double)SegmentCount;

        if (level < position)
        {
            return LevelColorPalette.OffBrush;
        }

        // インデックスの位置で発光色を決める（PlayMuse風：下位は緑、中位は黄、上位は赤）。
        return LevelColorPalette.ResolveColor(position);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
