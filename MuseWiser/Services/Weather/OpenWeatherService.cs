using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MuseWiser.Models.Weather;
using MuseWiser.Services.Settings;

namespace MuseWiser.Services.Weather;

/// <summary>
/// OpenWeather の Current Weather Data API を呼び出して天気情報を取得するサービス。
/// API キーは表示設定（settings.json）の "WeatherApiKey" から取得する。
/// </summary>
public sealed class OpenWeatherService : IWeatherService
{
    private const string BaseUrl = "https://api.openweathermap.org/data/2.5/weather";

    private static readonly string[] WindDirections16 =
    [
        "N", "NNE", "NE", "ENE",
        "E", "ESE", "SE", "SSE",
        "S", "SSW", "SW", "WSW",
        "W", "WNW", "NW", "NNW",
    ];

    private readonly HttpClient _httpClient;
    private readonly WeatherSettings _settings;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<OpenWeatherService> _logger;

    public OpenWeatherService(HttpClient httpClient, IOptions<WeatherSettings> settings, ISettingsService settingsService, ILogger<OpenWeatherService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<WeatherInfo?> GetCurrentWeatherAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = _settingsService.Load().WeatherApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("設定ファイルに OpenWeather APIキーが設定されていないため、天気情報を取得できません。");
            return null;
        }

        var requestUri = $"{BaseUrl}?q={Uri.EscapeDataString(_settings.City)}" +
                          $"&appid={apiKey}&units={_settings.Units}&lang={_settings.Language}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<OpenWeatherResponse>(requestUri, cancellationToken);
            if (response is null)
            {
                return null;
            }

            return MapToWeatherInfo(response);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogError(ex, "天気情報の取得に失敗しました。");
            return null;
        }
    }

    private static WeatherInfo MapToWeatherInfo(OpenWeatherResponse response)
    {
        var condition = response.Weather?.FirstOrDefault();

        return new WeatherInfo
        {
            CityName = response.Name ?? string.Empty,
            Temperature = response.Main?.Temp ?? 0,
            FeelsLike = response.Main?.FeelsLike ?? 0,
            Description = FormatDescription(condition?.Main, condition?.Description),
            Humidity = response.Main?.Humidity ?? 0,
            Pressure = response.Main?.Pressure ?? 0,
            RainVolume = response.Rain?.OneHour ?? 0,
            WindSpeed = response.Wind?.Speed ?? 0,
            WindDirection = ToCompassDirection(response.Wind?.Deg ?? 0),
            IconCode = condition?.Icon ?? string.Empty,
        };
    }

    private static string FormatDescription(string? main, string? description)
    {
        if (string.IsNullOrWhiteSpace(main))
        {
            return Capitalize(description) ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(description) ? main : $"{main} · {Capitalize(description)}";
    }

    private static string? Capitalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string ToCompassDirection(double degrees)
    {
        var normalized = ((degrees % 360) + 360) % 360;
        var index = (int)Math.Round(normalized / 22.5, MidpointRounding.AwayFromZero) % 16;
        return WindDirections16[index];
    }
}
