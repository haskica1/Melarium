using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class OrganizationRepository : Repository<Organization>, IOrganizationRepository
{
    public OrganizationRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Organization>> GetAllWithDetailsAsync() =>
        await _context.Organizations
            .AsNoTracking()
            .Include(o => o.Users)
            .Include(o => o.Apiaries)
            .Include(o => o.CreatedBy)
            .OrderBy(o => o.Name)
            .ToListAsync();

    public async Task<Organization?> GetWithDetailsAsync(int id) =>
        await _context.Organizations
            .Include(o => o.Users)
            .Include(o => o.Apiaries)
            .Include(o => o.CreatedBy)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<Dictionary<int, int>> GetBeehiveCountsAsync(int? organizationId = null) =>
        await _context.Beehives
            .AsNoTracking()
            // Merged-away hives are out of the apiary and out of this count (SPEC-19 §5 #11).
            // The "last activity" query below deliberately does not filter: a merge is a sign the
            // organization is being used (ADR-034).
            .Where(b => (organizationId == null || b.Apiary.OrganizationId == organizationId)
                        && b.MergedIntoBeehiveId == null)
            .GroupBy(b => b.Apiary.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.Count);

    /// <summary>
    /// One "GROUP BY organization, MAX(...)" per table, merged in memory. Deliberately not a single
    /// UNION: each of these translates to SQL every EF version supports, whereas grouping across a
    /// set operation is the kind of query that fails at runtime rather than at build time.
    ///
    /// <para><c>UpdatedAt ?? CreatedAt</c> is the "last touched" moment of a row.
    /// <c>MelariumDbContext.SaveChangesAsync</c> stamps <c>UpdatedAt</c> on every modification of
    /// every <c>BaseEntity</c>, so an edit counts as activity without any service having to
    /// cooperate — which is what makes this measurable from existing data at all.</para>
    ///
    /// <para>Tables not listed here are covered by one that is: an apiary move bumps its apiary,
    /// harvest and expense line items are written inside their parent's update, and a queen edit
    /// bumps the queen. The one deliberate omission is inspection photos — an upload lands on the
    /// same day as the inspection it belongs to.</para>
    /// </summary>
    public async Task<Dictionary<int, DateTime>> GetLastActivityAsync(int? organizationId = null)
    {
        var latest = new Dictionary<int, DateTime>();

        // Pčelinjak — also stands in for apiary moves (ApiaryMoveService bumps the apiary).
        Merge(latest, await _context.Apiaries.AsNoTracking()
            .Where(a => organizationId == null || a.OrganizationId == organizationId)
            .GroupBy(a => a.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(a => a.UpdatedAt ?? a.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Košnica
        Merge(latest, await _context.Beehives.AsNoTracking()
            .Where(b => organizationId == null || b.Apiary.OrganizationId == organizationId)
            .GroupBy(b => b.Apiary.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(b => b.UpdatedAt ?? b.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Pregled
        Merge(latest, await _context.Inspections.AsNoTracking()
            .Where(i => organizationId == null || i.Beehive.Apiary.OrganizationId == organizationId)
            .GroupBy(i => i.Beehive.Apiary.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(i => i.UpdatedAt ?? i.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Matica — a queen edit writes a QueenEditLog row and bumps the queen, so this covers both.
        Merge(latest, await _context.Queens.AsNoTracking()
            .Where(q => organizationId == null || q.Beehive.Apiary.OrganizationId == organizationId)
            .GroupBy(q => q.Beehive.Apiary.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(q => q.UpdatedAt ?? q.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Prehrana
        Merge(latest, await _context.Diets.AsNoTracking()
            .Where(d => organizationId == null || d.Apiary.OrganizationId == organizationId)
            .GroupBy(d => d.Apiary.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(d => d.UpdatedAt ?? d.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Hranjenje (odrađena runda) — the diet is defined once and then fed for weeks; ticking a
        // round off does not always bump the parent, so the rounds have to be looked at directly.
        Merge(latest, await _context.FeedingEntries.AsNoTracking()
            .Where(f => organizationId == null || f.Diet.Apiary.OrganizationId == organizationId)
            .GroupBy(f => f.Diet.Apiary.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(f => f.UpdatedAt ?? f.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Tretman
        Merge(latest, await _context.Treatments.AsNoTracking()
            .Where(t => organizationId == null || t.Apiary.OrganizationId == organizationId)
            .GroupBy(t => t.Apiary.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(t => t.UpdatedAt ?? t.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Runda tretmana — same reason as feeding rounds.
        Merge(latest, await _context.TreatmentRounds.AsNoTracking()
            .Where(r => organizationId == null || r.Treatment.Apiary.OrganizationId == organizationId)
            .GroupBy(r => r.Treatment.Apiary.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(r => r.UpdatedAt ?? r.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Vrcanje — entries are written inside the harvest's own create/update.
        Merge(latest, await _context.Harvests.AsNoTracking()
            .Where(h => organizationId == null || h.Apiary.OrganizationId == organizationId)
            .GroupBy(h => h.Apiary.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(h => h.UpdatedAt ?? h.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Trošak — items are written inside the expense's own create/update.
        Merge(latest, await _context.Expenses.AsNoTracking()
            .Where(e => organizationId == null || e.OrganizationId == organizationId)
            .GroupBy(e => e.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(e => e.UpdatedAt ?? e.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Pašnjak
        Merge(latest, await _context.Pastures.AsNoTracking()
            .Where(p => organizationId == null || p.OrganizationId == organizationId)
            .GroupBy(p => p.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(p => p.UpdatedAt ?? p.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Član
        Merge(latest, await _context.Users.AsNoTracking()
            .Where(u => u.OrganizationId != null)
            .Where(u => organizationId == null || u.OrganizationId == organizationId)
            .GroupBy(u => u.OrganizationId!.Value)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(u => u.UpdatedAt ?? u.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Zadatak na pčelinjaku — a todo carries exactly one of ApiaryId/BeehiveId, so the two
        // kinds are two queries rather than one conditional join EF would have to guess at.
        Merge(latest, await _context.Todos.AsNoTracking()
            .Where(t => t.ApiaryId != null)
            .Where(t => organizationId == null || t.Apiary!.OrganizationId == organizationId)
            .GroupBy(t => t.Apiary!.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(t => t.UpdatedAt ?? t.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Zadatak na košnici
        Merge(latest, await _context.Todos.AsNoTracking()
            .Where(t => t.BeehiveId != null)
            .Where(t => organizationId == null || t.Beehive!.Apiary.OrganizationId == organizationId)
            .GroupBy(t => t.Beehive!.Apiary.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(t => t.UpdatedAt ?? t.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        // Prijava / rotacija refresh tokena. CreatedAt, not UpdatedAt: a row is written when someone
        // signs in and on every rotation while the app is in use, whereas UpdatedAt also moves when
        // a token is revoked from the outside (session revoke), which is not the organization acting.
        // This is the one signal that counts *reading* the app, not only writing to it.
        Merge(latest, await _context.RefreshTokens.AsNoTracking()
            .Where(rt => rt.User.OrganizationId != null)
            .Where(rt => organizationId == null || rt.User.OrganizationId == organizationId)
            .GroupBy(rt => rt.User.OrganizationId!.Value)
            .Select(g => new { OrganizationId = g.Key, At = g.Max(rt => rt.CreatedAt) })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.At));

        return latest;
    }

    private static void Merge(Dictionary<int, DateTime> target, Dictionary<int, DateTime> source)
    {
        foreach (var (organizationId, at) in source)
            if (!target.TryGetValue(organizationId, out var current) || at > current)
                target[organizationId] = at;
    }
}
