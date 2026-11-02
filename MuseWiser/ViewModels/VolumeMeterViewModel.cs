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
    /// スペクトラムバンドごとの周波数ラベル（20Hz〜20kHz を対数スケールで配置）。
    /// XAML からの ItemsSource バインディング用。
    /// </summary>
    public static IReadOnlyList<string> SpectrumBandLabels { get; } = CreateSpectrumBandLabels();

    private const float SpectrumLabelMinFrequencyHz = 20f;
    private const float SpectrumLabelMaxFrequencyHz = 20000f;

    private static IReadOnlyList<string> CreateSpectrumBandLabels()
    {
        var labels = new string[SpectrumBandCount];
        var logMin = Math.Log(SpectrumLabelMinFrequencyHz);
        var logMax = Math.Log(SpectrumLabelMaxFrequencyHz);

        for (var i = 0; i < SpectrumBandCount; i++)
        {
            var frequency = i == SpectrumBandCount - 1
                ? SpectrumLabelMaxFrequencyHz
                : Math.Exp(logMin + (logMax - logMin) * i / SpectrumBandCount);
            labels[i] = FormatFrequency(frequency);
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
                : $"{kilo:0.#}k";
        }

        return $"{frequency:0}";
    }

    private const int SpectrumBandCount = 16;
    private const double SpectrumDecayPerTick = 0.12;

    private readonly double[] _spectrumTargets = new double[SpectrumBandCount];

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
        for (var i = 0; i < SpectrumBands.Count; i++)
        {
            SpectrumBands[i].Level = 0;
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
            var current = SpectrumBands[i].Level;
            var next = StepTowards(current, _spectrumTargets[i], SpectrumDecayPerTick);
            if (next != current)
            {
                SpectrumBands[i].Level = next;
            }
        }
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
/// スペクトラム 1 バンド分の周波数ラベルとレベル（0.0〜1.0）を保持する。
/// </summary>
public sealed partial class SpectrumBandViewModel : ViewModelBase
{
    public string Label { get; }

    [ObservableProperty]
    private double _level;

    public SpectrumBandViewModel(string label)
    {
        Label = label;
    }
}
