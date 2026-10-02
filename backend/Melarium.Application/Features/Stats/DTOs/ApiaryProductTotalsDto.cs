namespace Melarium.Application.Features.Stats.DTOs;

/// <summary>
/// An apiary's current-year bee products, per type (SPEC-30). <paramref name="ApiaryId"/> is null for
/// the row of shared records, which belong to the organization rather than to any apiary.
/// </summary>
public record ApiaryProductTotalsDto(int? ApiaryId, string ApiaryName, IReadOnlyList<ProductKgDto> Items);

/// <summary>A pasture's or a hive's current-year products, each on its own (SPEC-30).</summary>
public record NamedProductTotalsDto(string Name, IReadOnlyList<ProductKgDto> Items);
