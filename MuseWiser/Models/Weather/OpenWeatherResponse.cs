namespace MuseWiser.Models.Weather;

/// <summary>
/// OpenWeather API (Current Weather Data) のレスポンスをデシリアライズするための内部モデル。
/// </summary>
internal sealed class OpenWeatherResponse
{
    public OpenWeatherConditionItem[]? Weather { get; set; }

    public OpenWeatherMain? Main { get; set; }

    public OpenWeatherWind? Wind { get; set; }

    public OpenWeatherRain? Rain { get; set; }

    public string? Name { get; set; }
}

internal sealed class OpenWeatherConditionItem
{
    public string? Main { get; set; }

    public string? Description { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

internal sealed class OpenWeatherMain
{
    public double Temp { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("feels_like")]
    public double FeelsLike { get; set; }

    public int Humidity { get; set; }

    public double Pressure { get; set; }
}

internal sealed class OpenWeatherWind
{
    public double Speed { get; set; }

    public double Deg { get; set; }
}

internal sealed class OpenWeatherRain
{
    [System.Text.Json.Serialization.JsonPropertyName("1h")]
    public double OneHour { get; set; }
}
