namespace Melarium.Application.Features.Stats.DTOs;

public record MonthTempDto(string Month, double? AvgTemp, double? MinTemp, double? MaxTemp);
