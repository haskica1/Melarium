using Melarium.Application.Features.Weather.DTOs;

namespace Melarium.Application.Features.Weather;

public interface IWeatherService
{
    Task<WeatherForecastDto> GetForecastAsync(double latitude, double longitude);

    /// <summary>
    /// Returns the current temperature (°C) at the given coordinates, or null when
    /// the location has no coordinates or the weather API is unreachable.
    /// </summary>
    Task<double?> GetCurrentTemperatureAsync(double latitude, double longitude);
}
