using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Harvests.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Harvests;

/// <summary>
/// Harvest CRUD with apiary-scoped authorization (same matrix as apiary management):
/// SystemAdmin/OrganizationAdmin — all org apiaries; ApiaryAdmin — own apiary; Beekeeper — read-only,
/// and only harvests that contain at least one of their assigned hives.
/// </summary>
public class HarvestService : IHarvestService
{
    private readonly IUnitOfWork _uow;
    private readonly IAccessGuard _access;
    private readonly ICurrentUser _currentUser;

    public HarvestService(IUnitOfWork uow, IAccessGuard access, ICurrentUser currentUser)
    {
        _uow = uow;
        _access = access;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<HarvestDto>> GetAllAsync(int? apiaryId, int? beehiveId, int? year)
    {
        if (_currentUser.Role == UserRole.Beekeeper)
            return await GetForBeekeeperAsync(apiaryId, beehiveId, year);

        if (beehiveId is int bid)
        {
            await _access.EnsureCanAccessBeehiveAsync(bid);
            return (await _uow.Harvests.GetByBeehiveAsync(bid))
                .Where(h => year == null || h.Date.Year == year)
                .Select(ToListDto);
        }

        if (apiaryId is int aid)
        {
            await _access.EnsureCanManageApiaryAsync(aid);
            return (await _uow.Harvests.GetByApiaryAsync(aid, year)).Select(ToListDto);
        }

        return _currentUser.Role switch
        {
            UserRole.ApiaryAdmin when _currentUser.ApiaryId is int myApiary =>
                (await _uow.Harvests.GetByApiaryAsync(myApiary, year)).Select(ToListDto),
            UserRole.OrganizationAdmin when _currentUser.OrganizationId is int orgId =>
                (await _uow.Harvests.GetByOrganizationAsync(orgId, year)).Select(ToListDto),
            // SystemAdmin (no organization) must pass an apiary filter; nothing else to scope to.
            _ => [],
        };
    }

    private async Task<IEnumerable<HarvestDto>> GetForBeekeeperAsync(int? apiaryId, int? beehiveId, int? year)
    {
        var hiveIds = await _access.GetAssignedBeehiveIdsAsync();
        if (hiveIds.Count == 0) return [];

        var apiaryIds = await _access.GetAssignedApiaryIdsAsync();

        IEnumerable<Harvest> harvests;
        if (beehiveId is int bid)
        {
            if (!hiveIds.Contains(bid)) throw new ForbiddenAccessException();
            harvests = await _uow.Harvests.GetByBeehiveAsync(bid);
        }
        else if (apiaryId is int aid)
        {
            if (!apiaryIds.Contains(aid)) throw new ForbiddenAccessException();
            harvests = await _uow.Harvests.GetByApiaryAsync(aid, year);
        }
        else
        {
            var all = new List<Harvest>();
            foreach (var ap in apiaryIds)
                all.AddRange(await _uow.Harvests.GetByApiaryAsync(ap, year));
            harvests = all;
        }

        // Only harvests that include at least one of the beekeeper's hives (whole harvest visible).
        return harvests
            .Where(h => year == null || h.Date.Year == year)
            .Where(h => h.Entries.Any(e => hiveIds.Contains(e.BeehiveId)))
            .Select(ToListDto);
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
        await _access.EnsureCanManageApiaryAsync(dto.ApiaryId);
        await EnsureEntriesBelongToApiaryAsync(dto.ApiaryId, dto.Entries.Select(e => e.BeehiveId));

        var harvest = new Harvest
        {
            ApiaryId    = dto.ApiaryId,
            Date        = dto.Date,
            HoneyType   = dto.HoneyType,
            PricePerKg  = dto.PricePerKg,
            Notes       = dto.Notes,
            CreatedById = _currentUser.UserId,
            Entries     = dto.Entries.Select(ToEntity).ToList(),
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

        await _access.EnsureCanManageApiaryAsync(harvest.ApiaryId);
        await EnsureEntriesBelongToApiaryAsync(harvest.ApiaryId, dto.Entries.Select(e => e.BeehiveId));

        harvest.Date       = dto.Date;
        harvest.HoneyType  = dto.HoneyType;
        harvest.PricePerKg = dto.PricePerKg;
        harvest.Notes      = dto.Notes;

        // Replace the entry set — delete + recreate within one SaveChanges.
        harvest.Entries.Clear();
        foreach (var e in dto.Entries)
            harvest.Entries.Add(ToEntity(e));

        harvest.UpdatedAt = DateTime.UtcNow;

        await _uow.Harvests.UpdateAsync(harvest);
        await _uow.SaveChangesAsync();

        var updated = await _uow.Harvests.GetWithEntriesAsync(id);
        return ToDetailDto(updated!);
    }

    public async Task DeleteAsync(int id)
    {
        var harvest = await _uow.Harvests.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Harvest), id);

        await _access.EnsureCanManageApiaryAsync(harvest.ApiaryId);

        await _uow.Harvests.DeleteAsync(harvest);
        await _uow.SaveChangesAsync();
    }

    public async Task<HiveYieldDto> GetHiveYieldAsync(int beehiveId)
    {
        if (!await _uow.Beehives.ExistsAsync(beehiveId))
            throw new NotFoundException(nameof(Beehive), beehiveId);

        await _access.EnsureCanAccessBeehiveAsync(beehiveId);

        var totals = await _uow.Harvests.GetHiveYearlyTotalsAsync(beehiveId);
        var byYear = totals
            .OrderByDescending(kv => kv.Key)
            .Select(kv => new YearKgDto(kv.Key, kv.Value))
            .ToList();

        var currentSeason = totals.TryGetValue(DateTime.UtcNow.Year, out var kg) ? kg : 0m;
        return new HiveYieldDto(currentSeason, byYear);
    }

    // ── Authorization helpers ────────────────────────────────────────────────────

    private async Task EnsureCanReadAsync(Harvest harvest)
    {
        if (_currentUser.Role == UserRole.Beekeeper)
        {
            var hiveIds = await _access.GetAssignedBeehiveIdsAsync();
            if (!harvest.Entries.Any(e => hiveIds.Contains(e.BeehiveId)))
                throw new ForbiddenAccessException();
            return;
        }

        await _access.EnsureCanManageApiaryAsync(harvest.ApiaryId);
    }

    private async Task EnsureEntriesBelongToApiaryAsync(int apiaryId, IEnumerable<int> beehiveIds)
    {
        var apiaryHiveIds = (await _uow.Beehives.GetByApiaryIdAsync(apiaryId))
            .Select(b => b.Id)
            .ToHashSet();

        var invalid = beehiveIds.Where(hid => !apiaryHiveIds.Contains(hid)).Distinct().ToList();
        if (invalid.Count > 0)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["entries"] = [$"Košnice ne pripadaju odabranom pčelinjaku: {string.Join(", ", invalid)}."]
            });
    }

    // ── Mapping (manual — DTO carries computed Bosnian label + totals) ────────────

    private static HarvestEntry ToEntity(CreateHarvestEntryDto e) => new()
    {
        BeehiveId       = e.BeehiveId,
        QuantityKg      = e.QuantityKg,
        FramesExtracted = e.FramesExtracted,
    };

    private static T MapCommon<T>(T dto, Harvest h) where T : HarvestDto
    {
        dto.Id               = h.Id;
        dto.ApiaryId         = h.ApiaryId;
        dto.ApiaryName       = h.Apiary?.Name;
        dto.Date             = h.Date;
        dto.HoneyType        = h.HoneyType;
        dto.HoneyTypeName    = BsLabels.Label(h.HoneyType);
        dto.PricePerKg       = h.PricePerKg;
        dto.Notes            = h.Notes;
        dto.TotalKg          = h.Entries.Sum(e => e.QuantityKg);
        dto.EntryCount       = h.Entries.Count;
        dto.EstimatedRevenue = h.PricePerKg.HasValue
            ? h.Entries.Sum(e => e.QuantityKg) * h.PricePerKg.Value
            : null;
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
