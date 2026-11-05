using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuseWiser.Models;
using MuseWiser.Models.Enums;
using MuseWiser.Services.Interop;
using MuseWiser.Services.Settings;

namespace MuseWiser.ViewModels;

/// <summary>
/// 配置・サイズ設定画面（SettingsWindow）の ViewModel。
/// モニタ選択・アンカー選択・サイズ入力を行い、<see cref="ISettingsService"/> 経由で永続化する。
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private MonitorInfo? _selectedMonitor;

    [ObservableProperty]
    private ScreenAnchor _selectedAnchor;

    [ObservableProperty]
    private int _scalePercent;

    [ObservableProperty]
    private double _offsetX;

    [ObservableProperty]
    private double _offsetY;

    [ObservableProperty]
    private string? _weatherApiKey;

    [ObservableProperty]
    private int _weatherUpdateIntervalMinutes;

    [ObservableProperty]
    private bool _isTopmost;

    public bool IsBottommost
    {
        get => !IsTopmost;
        set => IsTopmost = !value;
    }

    partial void OnIsTopmostChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBottommost));
    }

    public IReadOnlyList<MonitorInfo> Monitors { get; }

    public IReadOnlyList<ScreenAnchor> Anchors { get; } = Enum.GetValues<ScreenAnchor>();

    /// <summary>
    /// 設定が保存されたときに発生するイベント。メインウィンドウへの再適用通知に使用する。
    /// </summary>
    public event EventHandler<DisplaySettings>? SettingsSaved;

    /// <summary>
    /// 設定画面を閉じる要求が発生したときに発生するイベント。
    /// </summary>
    public event EventHandler? CloseRequested;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        Monitors = MonitorHelper.GetMonitors();

        var current = _settingsService.Load();
        ApplyToFields(current);
    }

    private void ApplyToFields(DisplaySettings settings)
    {
        SelectedMonitor = Monitors.FirstOrDefault(m => m.DeviceName == settings.MonitorDeviceName)
            ?? Monitors.FirstOrDefault(m => m.IsPrimary)
            ?? Monitors.FirstOrDefault();
        SelectedAnchor = settings.Anchor;
        ScalePercent = (int)Math.Round(settings.Scale * 100, MidpointRounding.AwayFromZero);
        OffsetX = settings.OffsetX;
        OffsetY = settings.OffsetY;
        WeatherApiKey = settings.WeatherApiKey;
        WeatherUpdateIntervalMinutes = settings.WeatherUpdateIntervalMinutes;
        IsTopmost = settings.WindowTopmostMode == WindowTopmostMode.Topmost;
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new DisplaySettings
        {
            MonitorDeviceName = SelectedMonitor?.DeviceName,
            Anchor = SelectedAnchor,
            Scale = (ScalePercent > 0 ? ScalePercent : 100) / 100.0,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            WeatherApiKey = WeatherApiKey,
            WeatherUpdateIntervalMinutes = WeatherUpdateIntervalMinutes > 0 ? WeatherUpdateIntervalMinutes : 60,
            WindowTopmostMode = IsTopmost ? WindowTopmostMode.Topmost : WindowTopmostMode.Bottommost,
        };

        _settingsService.Save(settings);
        SettingsSaved?.Invoke(this, settings);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
