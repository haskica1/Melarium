namespace Melarium.Application.Features.Dashboard.DTOs;

/// <summary>
/// Three days of forecast for one apiary. <see cref="FrostPriority"/> is set when the season's frost
/// rule would warn — Critical in spring, nothing for ordinary winter frost (SPEC-29).
/// </summary>
public record ApiaryWeatherDto(
    int ApiaryId,
    string ApiaryName,
    IReadOnlyList<WeatherDayDto> Days,
    string? FrostPriority,
    double? FrostMinTemp);
