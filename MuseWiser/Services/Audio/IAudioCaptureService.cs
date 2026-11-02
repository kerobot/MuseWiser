namespace MuseWiser.Services.Audio;

/// <summary>
/// 既定の再生デバイスの音量レベル・スペクトラム取得を抽象化するサービス。
/// 共有再生時は WASAPI ループバックキャプチャ、将来的には PlayMuse との
/// プロセス間通信（排他再生時の波形連携）による実装への差し替えを想定する。
/// </summary>
public interface IAudioCaptureService : IDisposable
{
    /// <summary>
    /// 音声データが更新されたときに発生するイベント。
    /// </summary>
    event EventHandler<AudioCaptureDataEventArgs>? DataAvailable;

    /// <summary>
    /// キャプチャを開始する。
    /// </summary>
    void Start();

    /// <summary>
    /// キャプチャを停止する。
    /// </summary>
    void Stop();
}

/// <summary>
/// 音声キャプチャで得られたレベル・スペクトラム情報。
/// </summary>
/// <param name="LeftLevel">左チャンネルのレベル（0.0〜1.0）。</param>
/// <param name="RightLevel">右チャンネルのレベル（0.0〜1.0）。</param>
/// <param name="Spectrum">周波数バンドごとの振幅（0.0〜1.0）。</param>
public sealed record AudioCaptureDataEventArgs(float LeftLevel, float RightLevel, float[] Spectrum);
