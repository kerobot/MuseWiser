using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MuseWiser.Services.Audio;

namespace MuseWiser.ViewModels;

/// <summary>
/// 既定の再生デバイスの左右音量レベルを表示するための ViewModel。
/// WASAPI 共有ループバックキャプチャから得たレベル値を、UI スレッドで
/// 滑らかに減衰させながら公開する。
/// </summary>
public sealed partial class VolumeMeterViewModel : ViewModelBase, IDisposable
{
    private const double DecayPerTick = 0.08;

    /// <summary>
    /// メーター表示の下限（この dB 以下は消灯扱い）。
    /// </summary>
    private const double MinDecibels = -50.0;

    /// <summary>
    /// LED セグメント表示用のインデックス一覧（0〜15）。XAML からの ItemsSource バインディング用。
    /// </summary>
    public static IReadOnlyList<int> SegmentIndices { get; } = Enumerable.Range(0, Converters.LevelSegmentActiveMultiConverter.SegmentCount).ToArray();

    /// <summary>
    /// スペクトラムバンドのインデックス一覧（0〜15）。XAML からの ItemsSource バインディング用。
    /// </summary>
    public static IReadOnlyList<int> SpectrumBandIndices { get; } = Enumerable.Range(0, SpectrumBandCount).ToArray();

    /// <summary>
    /// スペクトラムバンド 1 列あたりのブロック（LED セグメント）インデックス一覧。
    /// 上から下（高レベル→低レベル）へ描画するため、降順（11〜0）で並べている。
    /// XAML からの ItemsSource バインディング用。
    /// </summary>
    public static IReadOnlyList<int> SpectrumSegmentIndices { get; } = Enumerable.Range(0, SpectrumBandViewModel.SegmentCount).Reverse().ToArray();

    /// <summary>
    /// 各スペクトラムバンドの開始周波数（Hz）。<see cref="Services.Audio.WasapiLoopbackAudioService"/> の
    /// バンド境界と一致させる必要がある。
    /// </summary>
    private static readonly double[] SpectrumBandStartFrequencies =
    [
        20, 30, 40, 70, 120, 190, 310, 490, 780, 1200, 1900, 3100, 5000, 7900, 12000, 20000
    ];

    /// <summary>
    /// スペクトラムバンドごとの周波数ラベル。SPAN 系アナライザーに準じた固定の目盛り
    /// （20, 30, 40, 70, 120, 190, 310, 490, 780, 1.2k, 1.9k, 3.1k, 5.0k, 7.9k, 12k, 20k）を使用する。
    /// XAML からの ItemsSource バインディング用。
    /// </summary>
    public static IReadOnlyList<string> SpectrumBandLabels { get; } = CreateSpectrumBandLabels();

    private static IReadOnlyList<string> CreateSpectrumBandLabels()
    {
        var labels = new string[SpectrumBandCount];

        for (var i = 0; i < SpectrumBandCount; i++)
        {
            labels[i] = FormatFrequency(SpectrumBandStartFrequencies[i]);
        }

        return labels;
    }

    private static string FormatFrequency(double frequency)
    {
        if (frequency >= 1000)
        {
            var kilo = frequency / 1000.0;
            return kilo >= 10
                ? $"{kilo:0}k"
                : $"{kilo:0.0}k";
        }

        return $"{frequency:0}";
    }

    private const int SpectrumBandCount = 16;
    private const double SpectrumDecayPerTick = 0.12;

    /// <summary>
    /// ピークホールドの保持時間（ティック数）。33ms ティックで約0.5秒相当。
    /// </summary>
    private const int PeakHoldTicks = 15;

    /// <summary>
    /// ピークホールド保持後の減衰量（1ティックあたり、0.0〜1.0 スケール）。
    /// 音量メーターの <see cref="DecayPerTick"/> と同程度のゆっくりした減衰にする。
    /// </summary>
    private const double PeakDecayPerTick = DecayPerTick;

    private readonly double[] _spectrumTargets = new double[SpectrumBandCount];
    private readonly double[] _spectrumPeakValues = new double[SpectrumBandCount];
    private readonly int[] _spectrumPeakHoldTicks = new int[SpectrumBandCount];

    /// <summary>
    /// 現在のスペクトラムレベル（0.0〜1.0）と周波数ラベルを保持する要素一覧。
    /// UI スレッドで減衰させながら公開する。
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<SpectrumBandViewModel> SpectrumBands { get; } = CreateSpectrumBands();

    private static System.Collections.ObjectModel.ObservableCollection<SpectrumBandViewModel> CreateSpectrumBands()
        => new(SpectrumBandLabels.Select(label => new SpectrumBandViewModel(label)));

    private readonly IAudioCaptureService _captureService;
    private readonly ILogger<VolumeMeterViewModel> _logger;
    private readonly DispatcherTimer _decayTimer;

    private double _leftTarget;
    private double _rightTarget;

    [ObservableProperty]
    private double _leftLevel;

    [ObservableProperty]
    private double _rightLevel;

    [ObservableProperty]
    private bool _isActive;

    public VolumeMeterViewModel(IAudioCaptureService captureService, ILogger<VolumeMeterViewModel> logger)
    {
        _captureService = captureService;
        _logger = logger;

        _decayTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33),
        };
        _decayTimer.Tick += OnDecayTick;
    }

    public void Start()
    {
        try
        {
            _captureService.DataAvailable += OnDataAvailable;
            _captureService.Start();
            _decayTimer.Start();
            IsActive = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "音量メーターの開始に失敗しました。既定の再生デバイスの排他利用などが原因の可能性があります。");
            IsActive = false;
        }
    }

    public void Stop()
    {
        _decayTimer.Stop();
        _captureService.DataAvailable -= OnDataAvailable;
        _captureService.Stop();
        IsActive = false;
        LeftLevel = 0;
        RightLevel = 0;
        _leftTarget = 0;
        _rightTarget = 0;
        Array.Clear(_spectrumTargets);
        Array.Clear(_spectrumPeakValues);
        Array.Clear(_spectrumPeakHoldTicks);
        for (var i = 0; i < SpectrumBands.Count; i++)
        {
            var band = SpectrumBands[i];
            band.Level = 0;
            band.ActiveSegmentCount = 0;
            band.PeakSegmentIndex = -1;
        }
    }

    private void OnDataAvailable(object? sender, AudioCaptureDataEventArgs e)
    {
        _leftTarget = ToMeterScale(e.LeftLevel);
        _rightTarget = ToMeterScale(e.RightLevel);

        var spectrum = e.Spectrum;
        var count = Math.Min(spectrum.Length, _spectrumTargets.Length);
        for (var i = 0; i < count; i++)
        {
            _spectrumTargets[i] = Math.Clamp(spectrum[i], 0f, 1f);
        }
    }

    /// <summary>
    /// 振幅のピーク値（0.0〜1.0 のリニアスケール）を dB に変換したうえで、
    /// <see cref="MinDecibels"/>〜0dB を 0.0〜1.0 のメーター表示値に正規化する。
    /// 人間の聴感はリニアではなく対数的なため、小さな音量でも LED が反応するようにする。
    /// </summary>
    private static double ToMeterScale(float linearPeak)
    {
        if (linearPeak <= 0f)
        {
            return 0.0;
        }

        var decibels = 20.0 * Math.Log10(linearPeak);
        if (decibels <= MinDecibels)
        {
            return 0.0;
        }

        return Math.Clamp((decibels - MinDecibels) / -MinDecibels, 0.0, 1.0);
    }

    private void OnDecayTick(object? sender, EventArgs e)
    {
        LeftLevel = StepTowards(LeftLevel, _leftTarget);
        RightLevel = StepTowards(RightLevel, _rightTarget);

        for (var i = 0; i < SpectrumBands.Count; i++)
        {
            var band = SpectrumBands[i];
            var next = StepTowards(band.Level, _spectrumTargets[i], SpectrumDecayPerTick);
            band.Level = next;

            // 連続値のレベルを 0〜SegmentCount の整数へ量子化する。
            // int プロパティへの代入は既定の等価比較により、値が変化しない限り
            // PropertyChanged が発火せず、無駄な再描画を避けられる。
            var quantizedLevel = (int)Math.Round(next * SpectrumBandViewModel.SegmentCount, MidpointRounding.AwayFromZero);
            band.ActiveSegmentCount = Math.Clamp(quantizedLevel, 0, SpectrumBandViewModel.SegmentCount);

            UpdateSpectrumPeak(band, i, quantizedLevel);
        }
    }

    /// <summary>
    /// スペクトラムバンドのピークホールド表示を更新する。
    /// 現在の量子化レベルが保持中のピークを超えたら即座にピークを更新し、
    /// 超えていなければ一定ティック数（<see cref="PeakHoldTicks"/>）だけ保持したうえで
    /// ゆっくり減衰させる（音量メーターの減衰と同程度）。
    /// </summary>
    private void UpdateSpectrumPeak(SpectrumBandViewModel band, int bandIndex, int quantizedLevel)
    {
        if (quantizedLevel >= _spectrumPeakValues[bandIndex])
        {
            _spectrumPeakValues[bandIndex] = quantizedLevel;
            _spectrumPeakHoldTicks[bandIndex] = PeakHoldTicks;
        }
        else if (_spectrumPeakHoldTicks[bandIndex] > 0)
        {
            _spectrumPeakHoldTicks[bandIndex]--;
        }
        else
        {
            var decayed = _spectrumPeakValues[bandIndex] - PeakDecayPerTick * SpectrumBandViewModel.SegmentCount;
            _spectrumPeakValues[bandIndex] = decayed < quantizedLevel ? quantizedLevel : decayed;
        }

        var peakLevel = (int)Math.Round(_spectrumPeakValues[bandIndex], MidpointRounding.AwayFromZero);
        band.PeakSegmentIndex = peakLevel > 0
            ? Math.Clamp(peakLevel, 1, SpectrumBandViewModel.SegmentCount) - 1
            : -1;
    }

    private static double StepTowards(double current, double target, double decayPerTick = DecayPerTick)
    {
        if (target >= current)
        {
            return target;
        }

        var next = current - decayPerTick;
        return next < target ? target : next;
    }

    public void Dispose()
    {
        Stop();
        _decayTimer.Tick -= OnDecayTick;
    }
}

/// <summary>
/// スペクトラム 1 バンド分の周波数ラベルとブロック（LED セグメント）表示用の状態を保持する。
/// 連続値のレベルは内部の減衰計算用に保持し、UI へは量子化済みの整数値のみを公開することで、
/// 点灯状態に変化がないティックでは <see cref="PropertyChanged"/> が発火せず、不要な再描画を避ける。
/// </summary>
public sealed partial class SpectrumBandViewModel : ViewModelBase
{
    /// <summary>
    /// スペクトラムバンド 1 列あたりのブロック（LED セグメント）数。
    /// </summary>
    public const int SegmentCount = 12;

    public string Label { get; }

    /// <summary>
    /// 内部の減衰計算用に保持する連続値のレベル（0.0〜1.0）。UI へは公開しない。
    /// </summary>
    internal double Level { get; set; }

    /// <summary>
    /// 点灯しているブロック数（0〜<see cref="SegmentCount"/>）。UI バインド対象。
    /// </summary>
    [ObservableProperty]
    private int _activeSegmentCount;

    /// <summary>
    /// ピークホールドで表示するブロックのインデックス（0〜<see cref="SegmentCount"/> - 1）。
    /// 非表示の場合は -1。UI バインド対象。
    /// </summary>
    [ObservableProperty]
    private int _peakSegmentIndex = -1;

    public SpectrumBandViewModel(string label)
    {
        Label = label;
    }
}
