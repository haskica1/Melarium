using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Harvests.DTOs;

/// <summary>
/// A hive's yield of every product per year (newest first), for the hive card (SPEC-30). Only per-hive
/// lines count — a record kept as one figure for the apiary or organization cannot be pinned on a hive.
/// </summary>
public record HiveHarvestSummaryDto(IReadOnlyList<HiveHarvestYearDto> ByYear);

public record HiveHarvestYearDto(int Year, IReadOnlyList<HarvestKgDto> Items);

/// <summary>One product's quantity. The enum travels with its label so a client can sort and translate by it.</summary>
public record HarvestKgDto(HiveProductType ProductType, string ProductTypeName, decimal Kg);
