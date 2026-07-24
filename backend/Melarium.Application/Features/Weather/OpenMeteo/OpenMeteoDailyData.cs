using System.Text.Json.Serialization;

namespace Melarium.Application.Features.Weather.OpenMeteo;

internal sealed class OpenMeteoDailyData
{
    [JsonPropertyName("time")]
    public List<string>? Time { get; set; }

    [JsonPropertyName("temperature_2m_max")]
    public List<double?>? Temperature2mMax { get; set; }

    [JsonPropertyName("temperature_2m_min")]
    public List<double?>? Temperature2mMin { get; set; }

    [JsonPropertyName("apparent_temperature_max")]
    public List<double?>? ApparentTemperatureMax { get; set; }

    [JsonPropertyName("apparent_temperature_min")]
    public List<double?>? ApparentTemperatureMin { get; set; }

    // Open-Meteo renamed this from "weathercode" to "weather_code" in API v1 (2024)
    [JsonPropertyName("weather_code")]
    public List<double?>? WeatherCode { get; set; }

    [JsonPropertyName("precipitation_sum")]
    public List<double?>? PrecipitationSum { get; set; }

    // Open-Meteo renamed this from "windspeed_10m_max" to "wind_speed_10m_max" in API v1 (2024)
    [JsonPropertyName("wind_speed_10m_max")]
    public List<double?>? WindSpeed10mMax { get; set; }

    [JsonPropertyName("precipitation_probability_max")]
    public List<double?>? PrecipitationProbabilityMax { get; set; }

    [JsonPropertyName("sunrise")]
    public List<string?>? Sunrise { get; set; }

    [JsonPropertyName("sunset")]
    public List<string?>? Sunset { get; set; }

    [JsonPropertyName("uv_index_max")]
    public List<double?>? UvIndexMax { get; set; }
}
