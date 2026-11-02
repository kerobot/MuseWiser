using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MuseWiser.Models.Weather;
using MuseWiser.Services.Weather;

namespace MuseWiser.ViewModels;

/// <summary>
/// 天気情報の取得・定期更新・表示用プロパティを保持する ViewModel。
/// </summary>
public sealed partial class WeatherViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly IWeatherService _weatherService;
    private readonly ILogger<WeatherViewModel> _logger;
    private readonly TimeSpan _updateInterval;
    private readonly CancellationTokenSource _cts = new();
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

    public WeatherViewModel(IWeatherService weatherService, IOptions<WeatherSettings> settings, ILogger<WeatherViewModel> logger)
    {
        _weatherService = weatherService;
        _logger = logger;
        _updateInterval = TimeSpan.FromMinutes(Math.Max(1, settings.Value.UpdateIntervalMinutes));
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "天気情報の更新中にエラーが発生しました。");
        }
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
