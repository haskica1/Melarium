using Melarium.Application.Features.Harvests.DTOs;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Harvests;

public interface IHarvestService
{
    /// <summary>
    /// Role-scoped list of harvests, optionally filtered by apiary, hive and/or year. Honey alone unless
    /// <paramref name="kind"/> or <paramref name="productType"/> asks for more — what a client older than
    /// SPEC-30 expects from this endpoint.
    /// </summary>
    Task<IEnumerable<HarvestDto>> GetAllAsync(
        int? apiaryId, int? beehiveId, int? year,
        HarvestKind kind = HarvestKind.Honey, HiveProductType? productType = null);

    Task<HarvestDetailDto> GetByIdAsync(int id);
    Task<HarvestDetailDto> CreateAsync(CreateHarvestDto dto);
    Task<HarvestDetailDto> UpdateAsync(int id, UpdateHarvestDto dto);
    Task DeleteAsync(int id);

    /// <summary>Season + per-year honey yield for a single hive (access = viewing the hive).</summary>
    Task<HiveYieldDto> GetHiveYieldAsync(int beehiveId);

    /// <summary>A hive's yield of every product per year, from its per-hive lines (SPEC-30).</summary>
    Task<HiveHarvestSummaryDto> GetHiveSummaryAsync(int beehiveId);
}
