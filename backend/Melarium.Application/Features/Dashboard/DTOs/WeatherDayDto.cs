namespace Melarium.Application.Features.Dashboard.DTOs;

public record WeatherDayDto(
    string Date,
    double? MinTemp,
    double? MaxTemp,
    double? PrecipitationProbability,
    int? WeatherCode);
