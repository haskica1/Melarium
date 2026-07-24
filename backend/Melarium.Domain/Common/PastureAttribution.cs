using Melarium.Domain.Entities;

namespace Melarium.Domain.Common;

/// <summary>
/// Pure yield-attribution rule (SPEC-10): a harvest belongs to the pasture the apiary was on at the
/// harvest date — the ToPasture of the latest move with <c>MovedAt &lt;= date</c> (same-day move →
/// the new pasture). No move before the date → null = "Matična lokacija". Single source of truth
/// for the stats aggregation and unit-tested directly.
/// </summary>
public static class PastureAttribution
{
    public static int? ResolveToPastureId(IEnumerable<ApiaryMove> moves, DateTime date) =>
        moves
            .Where(m => m.MovedAt.Date <= date.Date)
            .OrderByDescending(m => m.MovedAt)
            .ThenByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault()
            ?.ToPastureId;
}
