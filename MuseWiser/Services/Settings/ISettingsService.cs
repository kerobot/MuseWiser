using MuseWiser.Models;

namespace MuseWiser.Services.Settings;

/// <summary>
/// アプリケーション設定（配置位置・サイズ等）の読み込み・保存を行うサービス。
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// 現在の表示設定を取得する。
    /// </summary>
    DisplaySettings Load();

    /// <summary>
    /// 表示設定を保存する。
    /// </summary>
    void Save(DisplaySettings settings);
}
