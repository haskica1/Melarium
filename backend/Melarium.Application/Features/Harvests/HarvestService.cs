using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Harvests.DTOs;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Harvests;

/// <summary>
/// Harvests (prinosi): honey extractions and, since SPEC-30, every other bee product — with
/// apiary-scoped authorization (same matrix as apiary management): SystemAdmin/OrganizationAdmin — all
/// org apiaries; ApiaryAdmin — own apiary; Beekeeper — read-only, and only records that contain at least
/// one of their assigned hives.
///
/// <para>
/// A record of the <b>whole organization</b> (no apiary) is written by its OrganizationAdmin only,
/// read by its ApiaryAdmins, and never seen by a Beekeeper, whose scope is hives.
/// </para>
///
/// <para>
/// Honey is on every plan, as it has been since SPEC-02. Writing any other product needs Standard or
/// above (<see cref="PlanFeature.HiveProducts"/>); reading and deleting never do, so a Free organization
/// keeps what it recorded and a past season's report stays whole.
/// </para>
/// </summary>
public class HarvestService : IHarvestService
{
    private readonly IUnitOfWork _uow;
    private readonly IAccessGuard _access;
    private readonly IPlanLock _planLock;
    private readonly IPlanGuard _planGuard;
    private readonly ICurrentUser _currentUser;

    public HarvestService(
        IUnitOfWork uow,
        IAccessGuard access,
        ICurrentUser currentUser,
        IPlanLock planLock,
        IPlanGuard planGuard)
    {
        _uow = uow;
        _access = access;
        _planLock = planLock;
        _planGuard = planGuard;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<HarvestDto>> GetAllAsync(
        int? apiaryId, int? beehiveId, int? year,
        HarvestKind kind = HarvestKind.Honey, HiveProductType? productType = null)
    {
        // A named product narrows the query to its own side of the honey line.
        if (productType is HiveProductType named)
            kind = named == HiveProductType.Honey ? HarvestKind.Honey : HarvestKind.OtherProducts;

        return (await GetScopedAsync(apiaryId, beehiveId, year, kind))
            .Where(h => productType is not HiveProductType p || h.ProductType == p)
            .Select(ToListDto);
    }

    private async Task<IEnumerable<Harvest>> GetScopedAsync(int? apiaryId, int? beehiveId, int? year, HarvestKind kind)
    {
        if (_currentUser.Role == UserRole.Beekeeper)
            return await GetForBeekeeperAsync(apiaryId, beehiveId, year, kind);

        if (beehiveId is int bid)
        {
            await _access.EnsureCanAccessBeehiveAsync(bid);
            return (await _uow.Harvests.GetByBeehiveAsync(bid, kind))
                .Where(h => year == null || h.Date.Year == year);
        }

        if (apiaryId is int aid)
        {
            await _access.EnsureCanManageApiaryAsync(aid);
            return await _uow.Harvests.GetByApiaryAsync(aid, kind, year);
        }

        // Same as the treatment register (SPEC-24): a locked apiary's rows stay out of the org list.
        // Records of the whole organization have no apiary to be locked with.
        var locked = await _planLock.GetForCurrentUserAsync();

        switch (_currentUser.Role)
        {
            case UserRole.ApiaryAdmin when _currentUser.ApiaryId is int myApiary && _currentUser.OrganizationId is int orgId:
            {
                IEnumerable<Harvest> own = locked.ApiaryIds.Contains(myApiary)
                    ? []
                    : await _uow.Harvests.GetByApiaryAsync(myApiary, kind, year);
                var shared = await _uow.Harvests.GetSharedAsync(orgId, kind, year);
                return own.Concat(shared)
                    .OrderByDescending(h => h.Date)
                    .ThenByDescending(h => h.CreatedAt);
            }

            case UserRole.OrganizationAdmin when _currentUser.OrganizationId is int orgId:
                return (await _uow.Harvests.GetByOrganizationAsync(orgId, kind, year))
                    .Where(h => h.ApiaryId is not int a || !locked.ApiaryIds.Contains(a));

            // SystemAdmin (no organization) must pass an apiary filter; nothing else to scope to.
            default:
                return [];
        }
    }

    private async Task<IEnumerable<Harvest>> GetForBeekeeperAsync(int? apiaryId, int? beehiveId, int? year, HarvestKind kind)
    {
        var hiveIds = await _access.GetAssignedBeehiveIdsAsync();
        if (hiveIds.Count == 0) return [];

        // The lock applies to this path too (SPEC-24): a beekeeper assigned only to locked hives sees
        // nothing, rather than reading around the lock through the records their hives are in.
        var locked = await _planLock.GetForCurrentUserAsync();
        var visibleHiveIds = hiveIds.Where(id => !locked.BeehiveIds.Contains(id)).ToHashSet();

        IEnumerable<Harvest> harvests;
        if (beehiveId is int bid)
        {
            await _access.EnsureCanAccessBeehiveAsync(bid);
            harvests = await _uow.Harvests.GetByBeehiveAsync(bid, kind);
        }
        else if (apiaryId is int aid)
        {
            if (!(await _access.GetAssignedApiaryIdsAsync()).Contains(aid)) throw new ForbiddenAccessException();
            await _planLock.EnsureApiaryUnlockedAsync(aid);
            harvests = await _uow.Harvests.GetByApiaryAsync(aid, kind, year);
        }
        else
        {
            var all = new List<Harvest>();
            foreach (var ap in await _access.GetAssignedApiaryIdsAsync())
                all.AddRange(await _uow.Harvests.GetByApiaryAsync(ap, kind, year));
            harvests = all.OrderByDescending(h => h.Date).ThenByDescending(h => h.CreatedAt);
        }

        // The whole record is visible once one of their hives is in it — filtering its lines would make
        // the total lie. A record kept as one figure has no hives, so it never matches.
        return harvests
            .Where(h => year == null || h.Date.Year == year)
            .Where(h => h.ApiaryId is not int a || !locked.ApiaryIds.Contains(a))
            .Where(h => h.Entries.Any(e => visibleHiveIds.Contains(e.BeehiveId)));
    }

    public async Task<HarvestDetailDto> GetByIdAsync(int id)
    {
        var harvest = await _uow.Harvests.GetWithEntriesAsync(id)
            ?? throw new NotFoundException(nameof(Harvest), id);

        await EnsureCanReadAsync(harvest);
        return ToDetailDto(harvest);
    }

    public async Task<HarvestDetailDto> CreateAsync(CreateHarvestDto dto)
    {
        var product = dto.ProductType ?? HiveProductType.Honey;

        int organizationId;
        if (dto.ApiaryId is int apiaryId)
        {
            await _access.EnsureCanManageApiaryAsync(apiaryId);
            var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId)
                ?? throw new NotFoundException(nameof(Apiary), apiaryId);
            organizationId = apiary.OrganizationId;
        }
        else
        {
            organizationId = _currentUser.OrganizationId
                ?? throw new ForbiddenAccessException("Zapis za cijelu organizaciju postoji samo u okviru organizacije.");
            EnsureCanWriteShared(organizationId);
        }

        await EnsurePlanAllowsAsync(organizationId, product);

        if (dto.ApiaryId is int aid && dto.Entries.Count > 0)
            await EnsureEntriesBelongToApiaryAsync(aid, dto.Entries.Select(e => e.BeehiveId));

        var isHoney = product == HiveProductType.Honey;
        var harvest = new Harvest
        {
            OrganizationId = organizationId,
            ApiaryId       = dto.ApiaryId,
            Date           = dto.Date,
            ProductType    = product,
            HoneyType      = isHoney ? dto.HoneyType : null,
            PricePerKg     = dto.PricePerKg,
            BulkKg         = dto.Entries.Count > 0 ? null : dto.BulkKg,
            Notes          = dto.Notes,
            CreatedById    = _currentUser.UserId,
            Entries        = dto.Entries.Select(e => ToEntity(e, isHoney)).ToList(),
        };

        await _uow.Harvests.AddAsync(harvest);
        await _uow.SaveChangesAsync();

        var created = await _uow.Harvests.GetWithEntriesAsync(harvest.Id)
            ?? throw new InvalidOperationException("Harvest was not saved correctly.");
        return ToDetailDto(created);
    }

    public async Task<HarvestDetailDto> UpdateAsync(int id, UpdateHarvestDto dto)
    {
        var harvest = await _uow.Harvests.GetWithEntriesAsync(id)
            ?? throw new NotFoundException(nameof(Harvest), id);

        await EnsureCanWriteAsync(harvest);

        // Both sides count: Free keeps its earlier wax read-only, and cannot turn honey into wax either.
        var product = dto.ProductType ?? harvest.ProductType;
        await EnsurePlanAllowsAsync(harvest.OrganizationId, harvest.ProductType);
        await EnsurePlanAllowsAsync(harvest.OrganizationId, product);

        var isHoney = product == HiveProductType.Honey;
        if (isHoney && dto.HoneyType is null)
            throw Invalid("Vrsta meda je obavezna.");

        if (dto.Entries.Count > 0)
        {
            if (harvest.ApiaryId is not int apiaryId)
                throw Invalid("Zapis za cijelu organizaciju nema raspodjelu po košnicama — unesite ukupnu količinu.");
            await EnsureEntriesBelongToApiaryAsync(apiaryId, dto.Entries.Select(e => e.BeehiveId));
        }

        harvest.Date        = dto.Date;
        harvest.ProductType = product;
        harvest.HoneyType   = isHoney ? dto.HoneyType : null;
        harvest.PricePerKg  = dto.PricePerKg;
        harvest.BulkKg      = dto.Entries.Count > 0 ? null : dto.BulkKg;
        harvest.Notes       = dto.Notes;

        // Replace the entry set — delete + recreate within one SaveChanges. Switching between a
        // per-hive split and one figure lands here too.
        harvest.Entries.Clear();
        foreach (var e in dto.Entries)
            harvest.Entries.Add(ToEntity(e, isHoney));

        harvest.UpdatedAt = DateTime.UtcNow;

        await _uow.Harvests.UpdateAsync(harvest);
        await _uow.SaveChangesAsync();

        var updated = await _uow.Harvests.GetWithEntriesAsync(id);
        return ToDetailDto(updated!);
    }

    /// <summary>No plan gate: deleting is how a Free organization tidies what it can no longer edit.</summary>
    public async Task DeleteAsync(int id)
    {
        var harvest = await _uow.Harvests.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Harvest), id);

        await EnsureCanWriteAsync(harvest);

        await _uow.Harvests.DeleteAsync(harvest);
        await _uow.SaveChangesAsync();
    }

    public async Task<HiveYieldDto> GetHiveYieldAsync(int beehiveId)
    {
        if (!await _uow.Beehives.ExistsAsync(beehiveId))
            throw new NotFoundException(nameof(Beehive), beehiveId);

        await _access.EnsureCanAccessBeehiveAsync(beehiveId);

        var totals = (await _uow.Harvests.GetHiveTotalsByYearAsync(beehiveId))
            .Where(kv => kv.Key.ProductType == HiveProductType.Honey)
            .ToDictionary(kv => kv.Key.Year, kv => kv.Value);

        var byYear = totals
            .OrderByDescending(kv => kv.Key)
            .Select(kv => new YearKgDto(kv.Key, kv.Value))
            .ToList();

        var currentSeason = totals.TryGetValue(DateTime.UtcNow.Year, out var kg) ? kg : 0m;
        return new HiveYieldDto(currentSeason, byYear);
    }

    public async Task<HiveHarvestSummaryDto> GetHiveSummaryAsync(int beehiveId)
    {
        if (!await _uow.Beehives.ExistsAsync(beehiveId))
            throw new NotFoundException(nameof(Beehive), beehiveId);

        await _access.EnsureCanAccessBeehiveAsync(beehiveId);

        var byYear = (await _uow.Harvests.GetHiveTotalsByYearAsync(beehiveId))
            .GroupBy(kv => kv.Key.Year)
            .OrderByDescending(g => g.Key)
            .Select(g => new HiveHarvestYearDto(
                g.Key,
                g.OrderBy(kv => kv.Key.ProductType)
                 .Select(kv => new HarvestKgDto(kv.Key.ProductType, BsLabels.Label(kv.Key.ProductType), kv.Value))
                 .ToList()))
            .ToList();

        return new HiveHarvestSummaryDto(byYear);
    }

    // ── Authorization helpers ────────────────────────────────────────────────────

    private async Task EnsureCanReadAsync(Harvest harvest)
    {
        if (harvest.ApiaryId is not int apiaryId)
        {
            if (_currentUser.Role == UserRole.Beekeeper) throw new ForbiddenAccessException();
            _access.EnsureInOrganization(harvest.OrganizationId);
            return;
        }

        if (_currentUser.Role == UserRole.Beekeeper)
        {
            // Role before plan (403 before 402), so nobody learns about a plan that is not theirs.
            var hiveIds = await _access.GetAssignedBeehiveIdsAsync();
            if (!harvest.Entries.Any(e => hiveIds.Contains(e.BeehiveId)))
                throw new ForbiddenAccessException();
            await _planLock.EnsureApiaryUnlockedAsync(apiaryId);
            return;
        }

        await _access.EnsureCanManageApiaryAsync(apiaryId);
    }

    private async Task EnsureCanWriteAsync(Harvest harvest)
    {
        if (harvest.ApiaryId is int apiaryId)
            await _access.EnsureCanManageApiaryAsync(apiaryId);
        else
            EnsureCanWriteShared(harvest.OrganizationId);
    }

    /// <summary>
    /// A record of the whole organization speaks for all of it, so only its owner writes one. An
    /// ApiaryAdmin manages a single apiary and a Beekeeper only hives — neither may.
    /// </summary>
    private void EnsureCanWriteShared(int organizationId)
    {
        if (_currentUser.Role is not (UserRole.OrganizationAdmin or UserRole.SystemAdmin))
            throw new ForbiddenAccessException("Zapis za cijelu organizaciju može unijeti i mijenjati samo vlasnik organizacije.");

        _access.EnsureInOrganization(organizationId);
    }

    /// <summary>Honey has been on every plan since SPEC-02 and stays there; the other products are Standard+.</summary>
    private Task EnsurePlanAllowsAsync(int organizationId, HiveProductType product) =>
        product == HiveProductType.Honey
            ? Task.CompletedTask
            : _planGuard.EnsureFeatureAsync(organizationId, PlanFeature.HiveProducts);

    private async Task EnsureEntriesBelongToApiaryAsync(int apiaryId, IEnumerable<int> beehiveIds)
    {
        var apiaryHiveIds = (await _uow.Beehives.GetByApiaryIdAsync(apiaryId))
            .Select(b => b.Id)
            .ToHashSet();

        var invalid = beehiveIds.Where(hid => !apiaryHiveIds.Contains(hid)).Distinct().ToList();
        if (invalid.Count > 0)
            throw Invalid($"Košnice ne pripadaju odabranom pčelinjaku: {string.Join(", ", invalid)}.");
    }

    /// <summary>
    /// Keyed <c>detail</c> rather than by field: that is the key the frontend's error interceptor reads,
    /// so the Bosnian reason reaches the form instead of the generic "Validation Failed" title.
    /// </summary>
    private static ValidationException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["detail"] = [message] });

    // ── Mapping (manual — DTO carries computed Bosnian labels + derived totals) ───

    /// <summary>Frames extracted only mean something for honey.</summary>
    private static HarvestEntry ToEntity(CreateHarvestEntryDto e, bool isHoney) => new()
    {
        BeehiveId       = e.BeehiveId,
        QuantityKg      = e.QuantityKg,
        FramesExtracted = isHoney ? e.FramesExtracted : null,
    };

    private static T MapCommon<T>(T dto, Harvest h) where T : HarvestDto
    {
        dto.Id               = h.Id;
        dto.ApiaryId         = h.ApiaryId;
        dto.ApiaryName       = h.Apiary?.Name;
        dto.Date             = h.Date;
        dto.ProductType      = h.ProductType;
        dto.ProductTypeName  = BsLabels.Label(h.ProductType);
        dto.HoneyType        = h.HoneyType;
        dto.HoneyTypeName    = h.HoneyType is HoneyType t ? BsLabels.Label(t) : string.Empty;
        dto.PricePerKg       = h.PricePerKg;
        dto.BulkKg           = h.BulkKg;
        dto.Notes            = h.Notes;
        dto.TotalKg          = HarvestTotals.TotalKg(h);
        dto.EntryCount       = h.Entries.Count;
        dto.EstimatedRevenue = HarvestTotals.EstimatedRevenue(h);
        dto.CreatedByName    = h.CreatedBy is not null ? $"{h.CreatedBy.FirstName} {h.CreatedBy.LastName}" : null;
        dto.CreatedAt        = h.CreatedAt;
        return dto;
    }

    private static HarvestDto ToListDto(Harvest h) => MapCommon(new HarvestDto(), h);

    private static HarvestDetailDto ToDetailDto(Harvest h)
    {
        var dto = MapCommon(new HarvestDetailDto(), h);
        dto.Entries = h.Entries
            .OrderBy(e => e.Beehive != null ? e.Beehive.Name : string.Empty)
            .Select(e => new HarvestEntryDto
            {
                Id              = e.Id,
                BeehiveId       = e.BeehiveId,
                BeehiveName     = e.Beehive?.Name,
                QuantityKg      = e.QuantityKg,
                FramesExtracted = e.FramesExtracted,
            })
            .ToList();
        return dto;
    }
}
