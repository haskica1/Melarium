namespace Melarium.Application.Features.Harvests.DTOs;

/// <summary>Honey yield for a single hive: the current season total plus a per-year breakdown (newest first).</summary>
public record HiveYieldDto(decimal CurrentSeasonKg, IReadOnlyList<YearKgDto> ByYear);

public record YearKgDto(int Year, decimal Kg);
