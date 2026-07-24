using System.Text.Json.Serialization;

namespace Melarium.Application.Features.Weather.OpenMeteo;

/// <summary>Internal shape of the Open-Meteo current-conditions slice.</summary>
internal sealed class OpenMeteoCurrentResponse
{
    [JsonPropertyName("current")]
    public OpenMeteoCurrentData? Current { get; set; }
}

internal sealed class OpenMeteoCurrentData
{
    [JsonPropertyName("temperature_2m")]
    public double? Temperature2m { get; set; }

    [JsonPropertyName("apparent_temperature")]
    public double? ApparentTemperature { get; set; }

    [JsonPropertyName("weather_code")]
    public double? WeatherCode { get; set; }

    [JsonPropertyName("wind_speed_10m")]
    public double? WindSpeed10m { get; set; }

    [JsonPropertyName("relative_humidity_2m")]
    public double? RelativeHumidity2m { get; set; }
}
