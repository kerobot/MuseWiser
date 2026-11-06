using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MuseWiser.Models;

namespace MuseWiser.Services.Audio.PlayMuse;

/// <summary>
/// PlayMuse（別プロセスの音楽プレーヤー）との名前付きパイプ連携を想定した
/// <see cref="IAudioCaptureService"/> の拡張ポイント。
/// 現時点ではスタブ実装であり、実際のパイプ接続・受信処理は行わない。
/// 将来的に PlayMuse 側のプロトコルが確定した時点で、<see cref="Start"/> 内で
/// <see cref="System.IO.Pipes.NamedPipeClientStream"/> を用いた接続・受信ループを実装し、
/// 受信したレベル・スペクトラム値を <see cref="DataAvailable"/> イベントとして発行する。
/// </summary>
public sealed class PlayMuseNamedPipeAudioCaptureService : IAudioCaptureService
{
    private readonly PlayMusePipeOptions _options;
    private readonly ILogger<PlayMuseNamedPipeAudioCaptureService> _logger;

    public event EventHandler<AudioCaptureDataEventArgs>? DataAvailable;

    // TODO: PlayMuse 連携の実装時に、接続先デバイス名・サンプルレート・チャンネル数が判明した時点で
    // DeviceChanged イベントを発行する。
    public event EventHandler<AudioCaptureDeviceInfo>? DeviceChanged;

    public PlayMuseNamedPipeAudioCaptureService(
        IOptions<PlayMusePipeOptions> options,
        ILogger<PlayMuseNamedPipeAudioCaptureService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public void Start()
    {
        // TODO: PlayMuse 側のプロトコルが確定次第、以下を実装する。
        // 1. System.IO.Pipes.NamedPipeClientStream で _options.PipeName へ接続を試行する。
        // 2. 接続成功後、受信ループ（バックグラウンドタスク）でレベル・スペクトラムの
        //    メッセージを受信し、DataAvailable イベントとして発行する。
        // 3. 接続失敗・切断時は _options.ReconnectIntervalMilliseconds の間隔で再接続を試みる。
        // 4. PlayMuse が未起動の場合は例外を投げず、IsActive=false のまま待機する
        //    （VolumeMeterViewModel.Start の catch と同様の扱いにする）。
        _logger.LogInformation(
            "PlayMuse 連携（パイプ名: {PipeName}）は未実装のスタブです。WASAPI ループバックへフォールバックしてください。",
            _options.PipeName);
    }

    public void Stop()
    {
        // 現時点では接続を保持していないため何もしない。
    }

    public void Dispose()
    {
        // 現時点では解放すべきリソースを保持していない。
    }

    /// <summary>
    /// 将来のパイプ受信実装から呼び出すための補助メソッド。
    /// 受信したレベル・スペクトラム値を <see cref="DataAvailable"/> として発行する。
    /// </summary>
    private void RaiseDataAvailable(AudioCaptureDataEventArgs data)
    {
        DataAvailable?.Invoke(this, data);
    }
}
