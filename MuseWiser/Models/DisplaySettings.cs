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
    /// ウィンドウの幅（ピクセル）。
    /// </summary>
    public double Width { get; set; } = 620;

    /// <summary>
    /// ウィンドウの高さ（ピクセル）。
    /// </summary>
    public double Height { get; set; } = 600;
}
