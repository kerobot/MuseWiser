using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MuseWiser.Models.Weather;
using MuseWiser.Services.Settings;
using MuseWiser.Services.Weather;

namespace MuseWiser.ViewModels;

/// <summary>
/// 天気情報の取得・定期更新・表示用プロパティを保持する ViewModel。
/// </summary>
public sealed partial class WeatherViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly IWeatherService _weatherService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<WeatherViewModel> _logger;
    private TimeSpan _updateInterval;
    private CancellationTokenSource _cts = new();
    private Task? _updateLoopTask;

    [ObservableProperty]
    private string _cityName = string.Empty;

    [ObservableProperty]
    private string _temperatureText = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _feelsLikeText = string.Empty;

    [ObservableProperty]
    private string _humidityText = string.Empty;

    [ObservableProperty]
    private string _pressureText = string.Empty;

    [ObservableProperty]
    private string _rainVolumeText = string.Empty;

    [ObservableProperty]
    private string _windSpeedText = string.Empty;

    [ObservableProperty]
    private string _windDirectionText = string.Empty;

    [ObservableProperty]
    private string _feelsLikeValue = string.Empty;

    [ObservableProperty]
    private string _humidityValue = string.Empty;

    [ObservableProperty]
    private string _pressureValue = string.Empty;

    [ObservableProperty]
    private string _rainVolumeValue = string.Empty;

    [ObservableProperty]
    private string _windSpeedValue = string.Empty;

    [ObservableProperty]
    private string _windDirectionValue = string.Empty;

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private string _iconUrl = string.Empty;

    public WeatherViewModel(IWeatherService weatherService, ISettingsService settingsService, ILogger<WeatherViewModel> logger)
    {
        _weatherService = weatherService;
        _settingsService = settingsService;
        _logger = logger;
        _updateInterval = TimeSpan.FromMinutes(Math.Max(1, _settingsService.Load().WeatherUpdateIntervalMinutes));
    }

    /// <summary>
    /// 設定変更時などに即時で天気情報を再取得し、更新間隔も再読込して定期更新ループを再起動する。
    /// </summary>
    public void RefreshNow()
    {
        var intervalMinutes = Math.Max(1, _settingsService.Load().WeatherUpdateIntervalMinutes);
        var newInterval = TimeSpan.FromMinutes(intervalMinutes);

        if (newInterval == _updateInterval && _updateLoopTask is not null)
        {
            _ = RefreshAsync(_cts.Token);
            return;
        }

        _updateInterval = newInterval;
        RestartUpdateLoop();
    }

    private void RestartUpdateLoop()
    {
        var previousCts = _cts;
        var previousLoopTask = _updateLoopTask;

        _cts = new CancellationTokenSource();

        _updateLoopTask = RunUpdateLoopAsync(_cts.Token);

        _ = StopPreviousLoopAsync(previousCts, previousLoopTask);
    }

    private static async Task StopPreviousLoopAsync(CancellationTokenSource previousCts, Task? previousLoopTask)
    {
        previousCts.Cancel();

        if (previousLoopTask is not null)
        {
            try
            {
                await previousLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        previousCts.Dispose();
    }

    /// <summary>
    /// 定期更新ループを開始する（初回は即時取得）。
    /// </summary>
    public void Start()
    {
        _updateLoopTask = RunUpdateLoopAsync(_cts.Token);
    }

    private async Task RunUpdateLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_updateInterval);

        await RefreshAsync(cancellationToken);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // アプリ終了時のキャンセルは無視する。
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var weather = await _weatherService.GetCurrentWeatherAsync(cancellationToken);
            if (weather is not null)
            {
                Apply(weather);
            }
            else
            {
                Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "天気情報の更新中にエラーが発生しました。");
        }
    }

    private void Clear()
    {
        HasData = false;
        IconUrl = string.Empty;
    }

    private void Apply(WeatherInfo weather)
    {
        CityName = weather.CityName;
        TemperatureText = $"{weather.Temperature:F0}°C · {weather.CityName}";
        Description = weather.Description;
        FeelsLikeText = $"Feels like {weather.FeelsLike:F0}°C";
        HumidityText = $"Humidity {weather.Humidity}%";
        PressureText = $"Pressure {weather.Pressure:F0} hPa";
        RainVolumeText = $"Rain {weather.RainVolume:F1} mm";
        WindSpeedText = $"Wind {weather.WindSpeed.ToString("F2", CultureInfo.InvariantCulture)} m/s";
        WindDirectionText = $"Direction {weather.WindDirection}";
        FeelsLikeValue = $"{weather.FeelsLike:F0} °C";
        HumidityValue = $"{weather.Humidity} %";
        PressureValue = $"{weather.Pressure:F0} hPa";
        RainVolumeValue = $"{weather.RainVolume:F1} mm";
        WindSpeedValue = $"{weather.WindSpeed.ToString("F1", CultureInfo.InvariantCulture)} m/s";
        WindDirectionValue = weather.WindDirection;
        IconUrl = string.IsNullOrWhiteSpace(weather.IconCode)
            ? string.Empty
            : $"https://openweathermap.org/img/wn/{weather.IconCode}@2x.png";
        HasData = true;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_updateLoopTask is not null)
        {
            try
            {
                await _updateLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();
    }
}
