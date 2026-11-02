namespace MuseWiser.Services.Weather;

/// <summary>
/// OpenWeather API から天気情報を取得するサービス。
/// </summary>
public interface IWeatherService
{
    /// <summary>
    /// 指定した地点の現在の天気情報を取得する。
    /// </summary>
    Task<WeatherInfo?> GetCurrentWeatherAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 画面表示用に整形された天気情報。
/// </summary>
public sealed class WeatherInfo
{
    public string CityName { get; init; } = string.Empty;
    public double Temperature { get; init; }
    public double FeelsLike { get; init; }
    public string Description { get; init; } = string.Empty;
    public int Humidity { get; init; }
    public double Pressure { get; init; }
    public double RainVolume { get; init; }
    public double WindSpeed { get; init; }
    public string WindDirection { get; init; } = string.Empty;
}
