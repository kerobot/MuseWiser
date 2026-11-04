# MuseWiser

デスクトップに常駐する透明なオーバーレイ型ウィジェットアプリです。時計・天気情報・音量レベルメーター（L/R + スペクトラムバンド）を1つのウィンドウにまとめて表示します。WPF（.NET 10）製で、クリックスルー（背面操作透過）対応のトップモストウィンドウとして動作します。

## 主な機能

- **時計表示**：現在時刻（時:分:秒）と日付（曜日付き）をリアルタイム表示します（`ClockViewModel`）。
- **天気情報表示**：[OpenWeather](https://openweathermap.org/) の Current Weather Data API から現在の天気（気温・体感温度・湿度・気圧・降水量・風速・風向・天気アイコン）を取得して表示します（`WeatherViewModel` / `OpenWeatherService`）。
  - APIキーや更新間隔（分単位、既定60分）は設定画面から指定・変更でき、保存すると即座に再取得・再表示されます。
  - APIキー未設定時は天気情報欄を非表示にし、ウィンドウの高さも自動的に詰めます。
- **音量メーター表示**：既定の再生デバイスの音声を WASAPI ループバックキャプチャで取得し、L/R レベルメーターと16バンドのスペクトラムメーターをリアルタイム表示します（`VolumeMeterViewModel` / `WasapiLoopbackAudioService`）。
- **表示位置・サイズ設定**：配置先モニター、画面内アンカー位置、オフセット、表示倍率（%指定）を設定画面（`SettingsWindow`）から変更でき、`%AppData%\MuseWiser\settings.json` に永続化されます。
- **タスクトレイ常駐**：通知領域アイコンから設定画面の表示・アプリ終了を行えます。

## プロジェクト構成

```
MuseWiser/
├── App.xaml(.cs)                … アプリ起動・DIコンテナ構成・トレイアイコン制御
├── MainWindow.xaml(.cs)         … メインのオーバーレイウィンドウ（時計・天気・音量メーター）
├── SettingsWindow.xaml(.cs)     … 設定画面（配置・サイズ・天気APIキー等）
├── Models/                      … 設定・天気関連のデータモデル
├── Services/
│   ├── Audio/                   … 音声キャプチャ（WASAPIループバック／PlayMuse名前付きパイプ連携スタブ）
│   ├── Interop/                 … モニター情報取得、ウィンドウ位置・クリックスルー制御
│   ├── Settings/                … 表示設定の読み込み・保存
│   └── Weather/                 … OpenWeather API 連携
├── ViewModels/                  … 各機能のViewModel（MVVM, CommunityToolkit.Mvvm使用）
├── Converters/                  … レベルメーター・スペクトラム表示用の値コンバータ
└── appsettings.json             … 天気取得条件（都市・単位・言語）や音声ソース種別の既定設定
```

## 使用技術

- .NET 10 / WPF
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)（MVVMソースジェネレータ）
- `Microsoft.Extensions.Hosting` / `DependencyInjection` / `Options` によるホスティング・DI構成
- [Hardcodet.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon)（タスクトレイアイコン）
- OpenWeather Current Weather Data API

## 設定ファイル

- `appsettings.json`（アプリ同梱）：天気取得の既定条件（都市・単位・言語）、音声ソースの既定モードなど、ビルド時に固定する設定。
- `%AppData%\MuseWiser\settings.json`（実行時生成）：配置モニター・アンカー位置・オフセット・表示倍率・天気APIキー・天気更新間隔など、ユーザーが設定画面から変更する設定。

## 今後の実装予定

- **PlayMuse（[D:\Develop\Git\PlayMuse](D:\Develop\Git\PlayMuse)）とのプロセス間通信**：現在は `PlayMuseNamedPipeAudioCaptureService` が名前付きパイプ経由での音量レベル受信のスタブとして用意されていますが、これに加えて PlayMuse が再生中の楽曲情報（曲名・アーティスト名・再生状態など）を取得し、メインウィンドウ上に表示できるようにする予定です。PlayMuse側のプロトコル確定後、楽曲メタデータ用のメッセージ種別を追加し、対応する ViewModel・表示UIを実装します。
