namespace Melarium.Application.Features.Weather.DTOs;

public class DailyWeatherDto
{
    public string Date { get; set; } = string.Empty;
    public double? MaxTemp { get; set; }
    public double? MinTemp { get; set; }
    public double? ApparentTempMax { get; set; }
    public double? ApparentTempMin { get; set; }
    public int? WeatherCode { get; set; }
    public double? PrecipitationSum { get; set; }
    public double? MaxWindSpeed { get; set; }
    public double? PrecipitationProbability { get; set; }
    public string? Sunrise { get; set; }
    public string? Sunset { get; set; }
    public double? UvIndexMax { get; set; }
}
