using System.Net.Http.Json;
using Melarium.Application.Features.Weather.DTOs;
using Melarium.Application.Features.Weather.OpenMeteo;

namespace Melarium.Application.Features.Weather;

public class WeatherService : IWeatherService
{
    private readonly HttpClient _http;

    public WeatherService(HttpClient http) => _http = http;

    public async Task<WeatherForecastDto> GetForecastAsync(double latitude, double longitude)
    {
        var lat = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lon = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Open-Meteo is free and requires no API key.
        // NOTE: variable names changed in 2024 — "weathercode"→"weather_code", "windspeed_10m_max"→"wind_speed_10m_max"
        var url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={lat}&longitude={lon}" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min," +
            "apparent_temperature_max,apparent_temperature_min," +
            "precipitation_sum,precipitation_probability_max," +
            "wind_speed_10m_max,sunrise,sunset,uv_index_max" +
            "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m,relative_humidity_2m" +
            "&timezone=auto&forecast_days=7";

        var raw = await _http.GetFromJsonAsync<OpenMeteoResponse>(url)
            ?? throw new HttpRequestException("Empty response from weather API.");

        var forecast = new WeatherForecastDto
        {
            Latitude                   = raw.Latitude,
            Longitude                  = raw.Longitude,
            Timezone                   = raw.Timezone ?? "UTC",
            CurrentTemperature         = raw.Current?.Temperature2m,
            CurrentApparentTemperature = raw.Current?.ApparentTemperature,
            CurrentWeatherCode         = (int?)raw.Current?.WeatherCode,
            CurrentWindSpeed           = raw.Current?.WindSpeed10m,
            CurrentHumidity            = raw.Current?.RelativeHumidity2m,
        };

        var d = raw.Daily;
        int count = d.Time?.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            forecast.Daily.Add(new DailyWeatherDto
            {
                Date                     = d.Time![i],
                MaxTemp                  = SafeGet(d.Temperature2mMax, i),
                MinTemp                  = SafeGet(d.Temperature2mMin, i),
                ApparentTempMax          = SafeGet(d.ApparentTemperatureMax, i),
                ApparentTempMin          = SafeGet(d.ApparentTemperatureMin, i),
                WeatherCode              = (int?)SafeGet(d.WeatherCode, i),
                PrecipitationSum         = SafeGet(d.PrecipitationSum, i),
                MaxWindSpeed             = SafeGet(d.WindSpeed10mMax, i),
                PrecipitationProbability = SafeGet(d.PrecipitationProbabilityMax, i),
                Sunrise                  = SafeGetStr(d.Sunrise, i),
                Sunset                   = SafeGetStr(d.Sunset, i),
                UvIndexMax               = SafeGet(d.UvIndexMax, i),
            });
        }

        return forecast;
    }

    public async Task<double?> GetCurrentTemperatureAsync(double latitude, double longitude)
    {
        var lat = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lon = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={lat}&longitude={lon}" +
            "&current=temperature_2m&timezone=auto";

        var raw = await _http.GetFromJsonAsync<OpenMeteoCurrentResponse>(url);
        return raw?.Current?.Temperature2m;
    }

    private static double? SafeGet(List<double?>? list, int i) =>
        list is not null && i < list.Count ? list[i] : null;

    private static string? SafeGetStr(List<string?>? list, int i) =>
        list is not null && i < list.Count ? list[i] : null;
}
