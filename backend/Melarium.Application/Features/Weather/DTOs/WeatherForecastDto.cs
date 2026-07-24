namespace Melarium.Application.Features.Weather.DTOs;

public class WeatherForecastDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public double? CurrentTemperature { get; set; }
    public double? CurrentApparentTemperature { get; set; }
    public int? CurrentWeatherCode { get; set; }
    public double? CurrentWindSpeed { get; set; }
    public double? CurrentHumidity { get; set; }
    public List<DailyWeatherDto> Daily { get; set; } = [];
}
