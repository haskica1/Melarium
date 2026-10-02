using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Stats.DTOs;

/// <summary>
/// One bee product's current-year totals (SPEC-30). <paramref name="UnpricedKg"/> travels with the
/// revenue so a figure built only from priced records is never read as the whole income.
/// </summary>
public record ProductTypeTotalDto(
    HiveProductType ProductType,
    string Name,
    decimal Kg,
    decimal EstimatedRevenue,
    decimal UnpricedKg,
    int RecordCount);
