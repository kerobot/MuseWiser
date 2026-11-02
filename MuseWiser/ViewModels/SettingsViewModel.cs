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
    private double _width;

    [ObservableProperty]
    private double _height;

    [ObservableProperty]
    private double _offsetX;

    [ObservableProperty]
    private double _offsetY;

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
        Width = settings.Width;
        Height = settings.Height;
        OffsetX = settings.OffsetX;
        OffsetY = settings.OffsetY;
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new DisplaySettings
        {
            MonitorDeviceName = SelectedMonitor?.DeviceName,
            Anchor = SelectedAnchor,
            Width = Width,
            Height = Height,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
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
