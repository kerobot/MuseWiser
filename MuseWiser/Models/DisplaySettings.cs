namespace MuseWiser.Models;

using MuseWiser.Models.Enums;

/// <summary>
/// デスクトップ上でのウィンドウ表示位置・サイズに関する設定。
/// </summary>
public sealed class DisplaySettings
{
    /// <summary>
    /// 配置先モニタのデバイス名（<see cref="System.Windows.Forms.Screen.DeviceName"/> 相当）。
    /// 未指定の場合はプライマリモニタを使用する。
    /// </summary>
    public string? MonitorDeviceName { get; set; }

    /// <summary>
    /// モニタ内での配置アンカー。
    /// </summary>
    public ScreenAnchor Anchor { get; set; } = ScreenAnchor.TopCenter;

    /// <summary>
    /// アンカー基準位置からのXオフセット（ピクセル）。
    /// </summary>
    public double OffsetX { get; set; }

    /// <summary>
    /// アンカー基準位置からのYオフセット（ピクセル）。
    /// </summary>
    public double OffsetY { get; set; }

    /// <summary>
    /// デザイン基準サイズ（540×540）に対する表示倍率。
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// OpenWeather API キー。未設定の場合は天気情報欄を表示しない。
    /// </summary>
    public string? WeatherApiKey { get; set; }

    /// <summary>
    /// 天気情報の更新間隔（分）。未指定の場合は60分とする。
    /// </summary>
    public int WeatherUpdateIntervalMinutes { get; set; } = 60;
}
