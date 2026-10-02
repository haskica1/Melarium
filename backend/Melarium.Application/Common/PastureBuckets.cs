using Melarium.Domain.Common;
using Melarium.Domain.Entities;

namespace Melarium.Application.Common;

/// <summary>
/// Puts each harvest on the pasture its apiary stood on that day (<see cref="PastureAttribution"/>,
/// SPEC-10) — for the by-pasture tables of the stats page and the season report, honey and the other
/// products alike. A record of the whole organization stood on no one pasture, so it gets a bucket of
/// its own rather than the home location of an apiary it does not belong to (SPEC-30).
/// </summary>
public sealed class PastureBuckets
{
    public const string HomeLabel = "Matična lokacija";
    public const string SharedLabel = "Zajedničko";

    private readonly Dictionary<int, List<ApiaryMove>> _movesByApiary;
    private readonly Dictionary<int, string> _pastureNames;

    private PastureBuckets(List<ApiaryMove> moves)
    {
        _movesByApiary = moves.GroupBy(m => m.ApiaryId).ToDictionary(g => g.Key, g => g.ToList());
        _pastureNames = moves
            .Select(m => m.ToPasture)
            .Where(p => p is not null)
            .DistinctBy(p => p!.Id)
            .ToDictionary(p => p!.Id, p => p!.Name);
    }

    /// <summary>Null when the organization records no moves — then there is no by-pasture table at all.</summary>
    public static PastureBuckets? From(IEnumerable<ApiaryMove> moves)
    {
        var list = moves.ToList();
        return list.Count == 0 ? null : new PastureBuckets(list);
    }

    public PastureBucket Of(Harvest harvest) => harvest.ApiaryId is int apiaryId
        ? new PastureBucket(
            PastureAttribution.ResolveToPastureId(
                _movesByApiary.TryGetValue(apiaryId, out var moves) ? moves : [], harvest.Date),
            Shared: false)
        : new PastureBucket(null, Shared: true);

    public string NameOf(PastureBucket bucket) =>
        bucket.Shared ? SharedLabel
        : bucket.PastureId is int id ? (_pastureNames.TryGetValue(id, out var name) ? name : $"Pašnjak #{id}")
        : HomeLabel;

    /// <summary>
    /// Table order where kg cannot decide it (one row holds several products): pastures by name, then
    /// the home location, then the organization's own records.
    /// </summary>
    public IOrderedEnumerable<T> Order<T>(IEnumerable<T> rows, Func<T, PastureBucket> bucketOf) =>
        rows.OrderBy(r => bucketOf(r).Shared)
            .ThenBy(r => bucketOf(r).PastureId is null)
            .ThenBy(r => NameOf(bucketOf(r)), NaturalComparer.Instance);
}

/// <summary>Where a harvest is counted in a by-pasture table: a pasture, the home location (no pasture), or the organization's own row.</summary>
public readonly record struct PastureBucket(int? PastureId, bool Shared);
