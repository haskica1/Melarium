using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Diets.DTOs;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Diets;

/// <summary>
/// Feeding programme (diet) CRUD with apiary-scoped authorization, mirroring treatments: managers
/// write within scope; a Beekeeper reads programmes containing at least one of their assigned hives
/// and may tick a feeding round, but no longer creates or edits programmes (SPEC-12) — under an
/// apiary-scoped model that would mean choosing hives they cannot see.
/// </summary>
public class DietService : IDietService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessGuard _access;
    private readonly Common.Security.IPlanLock _planLock;

    public DietService(IUnitOfWork uow, ICurrentUser currentUser, IAccessGuard access, Common.Security.IPlanLock planLock)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _access      = access;
        _planLock    = planLock;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<IEnumerable<DietDto>> GetAllAsync(int? apiaryId, int? beehiveId, int? year)
    {
        if (_currentUser.Role == UserRole.Beekeeper)
            return await MapDietsWithCostAsync(await GetForBeekeeperAsync(apiaryId, beehiveId, year));

        if (beehiveId is int bid)
        {
            await _access.EnsureCanAccessBeehiveAsync(bid);
            var diets = (await _uow.Diets.GetByBeehiveAsync(bid))
                .Where(d => year == null || d.StartDate.Year == year);
            return await MapDietsWithCostAsync(diets);
        }

        if (apiaryId is int aid)
        {
            await _access.EnsureCanManageApiaryAsync(aid);
            return await MapDietsWithCostAsync(await _uow.Diets.GetByApiaryAsync(aid, year));
        }

        // Same as the treatment and harvest lists (SPEC-24): a locked apiary's rows stay out.
        var locked = await _planLock.GetForCurrentUserAsync();

        var byRole = _currentUser.Role switch
        {
            UserRole.ApiaryAdmin when _currentUser.ApiaryId is int myApiary =>
                locked.ApiaryIds.Contains(myApiary)
                    ? Enumerable.Empty<Diet>()
                    : await _uow.Diets.GetByApiaryAsync(myApiary, year),
            UserRole.OrganizationAdmin when _currentUser.OrganizationId is int orgId =>
                (await _uow.Diets.GetByOrganizationAsync(orgId, year))
                    .Where(d => !locked.ApiaryIds.Contains(d.ApiaryId)),
            _ => Enumerable.Empty<Diet>(), // SystemAdmin (no org) must pass a filter
        };
        return await MapDietsWithCostAsync(byRole);
    }

    private async Task<List<Diet>> GetForBeekeeperAsync(int? apiaryId, int? beehiveId, int? year)
    {
        var hiveIds = await _access.GetAssignedBeehiveIdsAsync();
        if (hiveIds.Count == 0) return [];

        var apiaryIds = await _access.GetAssignedApiaryIdsAsync();

        IEnumerable<Diet> diets;
        if (beehiveId is int bid)
        {
            if (!hiveIds.Contains(bid)) throw new ForbiddenAccessException();
            diets = await _uow.Diets.GetByBeehiveAsync(bid);
        }
        else if (apiaryId is int aid)
        {
            // They can reach the apiary; an apiary with nothing of theirs on a programme is empty, not 403.
            if (!apiaryIds.Contains(aid)) throw new ForbiddenAccessException();
            diets = await _uow.Diets.GetByApiaryAsync(aid, year);
        }
        else
        {
            diets = await _uow.Diets.GetByApiaryIdsAsync(apiaryIds);
        }

        return diets
            .Where(d => year == null || d.StartDate.Year == year)
            .Where(d => d.Beehives.Any(db => db.RemovedOn == null && hiveIds.Contains(db.BeehiveId)))
            .ToList();
    }

    public async Task<IEnumerable<DietActiveInfoDto>> GetActiveAsync(int apiaryId)
    {
        var hiveIds = await ResolveReadableHivesOfApiaryAsync(apiaryId);
        if (hiveIds.Count == 0) return [];

        var byHive = await _uow.Diets.GetActiveForBeehivesAsync(hiveIds);

        return byHive.Values
            .SelectMany(list => list)
            .Select(MapToActiveInfoDto)
            .ToList();
    }

    public async Task<DietDetailDto> GetByIdAsync(int id)
    {
        var diet = await _uow.Diets.GetWithEntriesAsync(id)
            ?? throw new NotFoundException(nameof(Diet), id);

        await EnsureCanReadAsync(diet);

        return await MapDietDetailWithCostAsync(diet);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public async Task<DietDetailDto> CreateAsync(CreateDietDto dto)
    {
        if (!await _uow.Apiaries.ExistsAsync(dto.ApiaryId))
            throw new NotFoundException(nameof(Apiary), dto.ApiaryId);

        await _access.EnsureCanManageApiaryAsync(dto.ApiaryId);
        await EnsureBeehivesBelongToApiaryAsync(dto.ApiaryId, dto.BeehiveIds);

        var diet = new Diet
        {
            ApiaryId       = dto.ApiaryId,
            Name           = dto.Name.Trim(),
            StartDate      = dto.StartDate.Date,
            Reason         = dto.Reason,
            CustomReason   = dto.CustomReason,
            DurationDays   = dto.DurationDays,
            FrequencyDays  = dto.FrequencyDays,
            FoodType       = dto.FoodType,
            CustomFoodType = dto.CustomFoodType,
            AmountPerHive  = dto.AmountPerHive,
            AmountUnit     = dto.AmountUnit,
            AmountNote     = string.IsNullOrWhiteSpace(dto.AmountNote) ? null : dto.AmountNote.Trim(),
            Status         = CalculateInitialStatus(dto.StartDate),
            CreatedById    = _currentUser.UserId,
        };

        diet.FeedingEntries = GenerateEntries(diet.StartDate, dto.DurationDays, dto.FrequencyDays);
        diet.Beehives = dto.BeehiveIds
            .Select(bid => new DietBeehive { BeehiveId = bid })
            .ToList();

        await _uow.Diets.AddAsync(diet);
        await _uow.SaveChangesAsync();

        var saved = await _uow.Diets.GetWithEntriesAsync(diet.Id)
            ?? throw new InvalidOperationException("Diet was not saved correctly.");

        return await MapDietDetailWithCostAsync(saved);
    }

    public async Task<DietDetailDto> UpdateAsync(int id, UpdateDietDto dto)
    {
        var diet = await _uow.Diets.GetWithEntriesAsync(id)
            ?? throw new NotFoundException(nameof(Diet), id);

        await _access.EnsureCanManageApiaryAsync(diet.ApiaryId);

        if (diet.Status is DietStatus.Completed or DietStatus.StoppedEarly)
            throw new BusinessRuleException("A completed or stopped diet cannot be updated.");

        diet.Name           = dto.Name.Trim();
        diet.StartDate      = dto.StartDate.Date;
        diet.Reason         = dto.Reason;
        diet.CustomReason   = dto.CustomReason;
        diet.DurationDays   = dto.DurationDays;
        diet.FrequencyDays  = dto.FrequencyDays;
        diet.FoodType       = dto.FoodType;
        diet.CustomFoodType = dto.CustomFoodType;
        diet.AmountPerHive  = dto.AmountPerHive;
        diet.AmountUnit     = dto.AmountUnit;
        diet.AmountNote     = string.IsNullOrWhiteSpace(dto.AmountNote) ? null : dto.AmountNote.Trim();
        diet.UpdatedAt      = DateTime.UtcNow;

        // Recalculate entries: keep completed, replace all pending
        RecalculateEntries(diet);

        // Recalculate status
        diet.Status = CalculateStatus(diet);

        await _uow.Diets.UpdateAsync(diet);
        await _uow.SaveChangesAsync();

        var saved = await _uow.Diets.GetWithEntriesAsync(id)
            ?? throw new InvalidOperationException("Failed to reload diet after update.");

        return await MapDietDetailWithCostAsync(saved);
    }

    public async Task DeleteAsync(int id)
    {
        var diet = await _uow.Diets.GetWithEntriesAsync(id)
            ?? throw new NotFoundException(nameof(Diet), id);

        await _access.EnsureCanManageApiaryAsync(diet.ApiaryId);

        var today = DateTime.UtcNow.Date;
        var hasCompletedEntries = diet.FeedingEntries.Any(e => e.Status == FeedingEntryStatus.Completed);
        var hasStarted = diet.StartDate.Date <= today;

        if (hasStarted || hasCompletedEntries)
            throw new BusinessRuleException(
                "A diet can only be deleted before it has started (no completed feedings and start date not yet reached).");

        await _uow.Diets.DeleteAsync(diet);
        await _uow.SaveChangesAsync();
    }

    public async Task<DietDetailDto> AddBeehivesAsync(int id, AddDietBeehivesDto dto)
    {
        var diet = await _uow.Diets.GetWithEntriesAsync(id)
            ?? throw new NotFoundException(nameof(Diet), id);

        await _access.EnsureCanManageApiaryAsync(diet.ApiaryId);

        if (diet.Status is DietStatus.Completed or DietStatus.StoppedEarly)
            throw new BusinessRuleException("Košnice se ne mogu dodati na završen ili prekinut program.");

        await EnsureBeehivesBelongToApiaryAsync(diet.ApiaryId, dto.BeehiveIds);

        var alreadyOn = diet.Beehives
            .Where(db => db.RemovedOn == null)
            .Select(db => db.BeehiveId)
            .ToHashSet();

        // A hive that is already on the programme is a no-op, not an error: the modal builds its list
        // from a possibly stale read, and re-adding is exactly what the partial unique index allows.
        foreach (var bid in dto.BeehiveIds.Where(bid => !alreadyOn.Contains(bid)))
            diet.Beehives.Add(new DietBeehive { DietId = diet.Id, BeehiveId = bid });

        diet.UpdatedAt = DateTime.UtcNow;

        await _uow.Diets.UpdateAsync(diet);
        await _uow.SaveChangesAsync();

        var saved = await _uow.Diets.GetWithEntriesAsync(id)
            ?? throw new InvalidOperationException("Failed to reload diet after adding hives.");

        return await MapDietDetailWithCostAsync(saved);
    }

    public async Task<DietDetailDto> RemoveBeehiveAsync(int id, int beehiveId)
    {
        var diet = await _uow.Diets.GetWithEntriesAsync(id)
            ?? throw new NotFoundException(nameof(Diet), id);

        // Removal is allowed in any status — it is a correction of the record, not field work.
        await _access.EnsureCanManageApiaryAsync(diet.ApiaryId);

        var link = diet.Beehives.FirstOrDefault(db => db.BeehiveId == beehiveId && db.RemovedOn == null)
            ?? throw new NotFoundException(nameof(DietBeehive), beehiveId);

        // Always today, never a client-supplied date — that is what keeps RemovedOn from landing in
        // the future or before the programme started.
        link.RemovedOn = DateTime.UtcNow.Date;
        link.UpdatedAt = DateTime.UtcNow;
        diet.UpdatedAt = DateTime.UtcNow;

        await _uow.Diets.UpdateAsync(diet);
        await _uow.SaveChangesAsync();

        var saved = await _uow.Diets.GetWithEntriesAsync(id)
            ?? throw new InvalidOperationException("Failed to reload diet after removing a hive.");

        return await MapDietDetailWithCostAsync(saved);
    }

    public async Task<DietDetailDto> CompleteEarlyAsync(int id, CompleteEarlyDto dto)
    {
        var diet = await _uow.Diets.GetWithEntriesAsync(id)
            ?? throw new NotFoundException(nameof(Diet), id);

        await _access.EnsureCanManageApiaryAsync(diet.ApiaryId);

        if (diet.Status is DietStatus.Completed or DietStatus.StoppedEarly)
            throw new BusinessRuleException("Diet is already finished.");

        if (string.IsNullOrWhiteSpace(dto.Comment))
            throw new BusinessRuleException("A comment is required when completing a diet early.");

        diet.Status                 = DietStatus.StoppedEarly;
        diet.EarlyCompletionComment = dto.Comment;
        diet.UpdatedAt              = DateTime.UtcNow;

        await _uow.Diets.UpdateAsync(diet);
        await _uow.SaveChangesAsync();

        var saved = await _uow.Diets.GetWithEntriesAsync(id)
            ?? throw new InvalidOperationException("Failed to reload diet after completing early.");
        return await MapDietDetailWithCostAsync(saved);
    }

    public async Task<FeedingEntryDto> CompleteFeedingEntryAsync(int dietId, int entryId, CompleteFeedingEntryDto dto)
    {
        var diet = await _uow.Diets.GetWithEntriesAsync(dietId)
            ?? throw new NotFoundException(nameof(Diet), dietId);

        // One tick closes the round for the whole group, including hives the caller is not assigned
        // to. Feeding is field work the Beekeeper physically performs — making them ask an admin to
        // record it would be worse than what this replaces.
        await EnsureCanTickRoundAsync(diet);

        if (diet.Status is DietStatus.Completed or DietStatus.StoppedEarly)
            throw new BusinessRuleException("Cannot mark feedings on a finished diet.");

        var entry = diet.FeedingEntries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new NotFoundException(nameof(FeedingEntry), entryId);

        if (entry.Status == FeedingEntryStatus.Completed)
            throw new BusinessRuleException("This feeding entry is already completed.");

        entry.Status         = FeedingEntryStatus.Completed;
        entry.CompletionDate = DateTime.UtcNow;
        entry.Note           = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        entry.UpdatedAt      = DateTime.UtcNow;

        // If all entries are now done, mark the diet as completed
        if (diet.FeedingEntries.All(e => e.Status == FeedingEntryStatus.Completed))
        {
            diet.Status    = DietStatus.Completed;
            diet.UpdatedAt = DateTime.UtcNow;
        }
        else if (diet.Status == DietStatus.NotStarted)
        {
            diet.Status    = DietStatus.InProgress;
            diet.UpdatedAt = DateTime.UtcNow;
        }

        await _uow.Diets.UpdateAsync(diet);
        await _uow.SaveChangesAsync();

        return MapToFeedingEntryDto(entry);
    }

    // ── Authorization helpers ─────────────────────────────────────────────────

    /// <summary>Read access: managers within scope, or a Beekeeper on at least one active hive.</summary>
    private async Task EnsureCanReadAsync(Diet diet)
    {
        if (_currentUser.Role == UserRole.Beekeeper)
        {
            var hiveIds = await _access.GetAssignedBeehiveIdsAsync();
            if (!diet.Beehives.Any(db => db.RemovedOn == null && hiveIds.Contains(db.BeehiveId)))
                throw new ForbiddenAccessException();
            return;
        }

        await _access.EnsureCanManageApiaryAsync(diet.ApiaryId);
    }

    /// <summary>Same rule as reading — a Beekeeper who may see the programme may record its rounds.</summary>
    private Task EnsureCanTickRoundAsync(Diet diet) => EnsureCanReadAsync(diet);

    /// <summary>The hives of an apiary the caller may see: all of them for managers, the assigned ones for a Beekeeper.</summary>
    private async Task<List<int>> ResolveReadableHivesOfApiaryAsync(int apiaryId)
    {
        var apiaryHiveIds = (await _uow.Beehives.GetByApiaryIdAsync(apiaryId)).Select(b => b.Id).ToList();

        if (_currentUser.Role != UserRole.Beekeeper)
        {
            await _access.EnsureCanManageApiaryAsync(apiaryId);
            return apiaryHiveIds;
        }

        var assigned = await _access.GetAssignedBeehiveIdsAsync();
        var mine = apiaryHiveIds.Where(assigned.Contains).ToList();
        if (mine.Count == 0) throw new ForbiddenAccessException();
        return mine;
    }

    private async Task EnsureBeehivesBelongToApiaryAsync(int apiaryId, IEnumerable<int> beehiveIds)
    {
        var apiaryHiveIds = (await _uow.Beehives.GetByApiaryIdAsync(apiaryId))
            .Select(b => b.Id)
            .ToHashSet();

        var invalid = beehiveIds.Where(hid => !apiaryHiveIds.Contains(hid)).Distinct().ToList();
        if (invalid.Count > 0)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["beehiveIds"] = [$"Košnice ne pripadaju odabranom pčelinjaku: {string.Join(", ", invalid)}."]
            });
    }

    // ── Feeding cost (SPEC-12 Phase E) ───────────────────────────────────────────

    /// <summary>Money is not visible to everyone: a Beekeeper has no access to the Expenses module at all.</summary>
    private bool ShowCostToCaller() => _currentUser.Role != UserRole.Beekeeper;

    private async Task<Dictionary<int, List<(string Currency, decimal Total)>>> LoadCostTotalsAsync(IEnumerable<int> dietIds) =>
        await _uow.Expenses.GetTotalsByDietsAsync(dietIds);

    private async Task<List<DietDto>> MapDietsWithCostAsync(IEnumerable<Diet> diets)
    {
        var list = diets as List<Diet> ?? diets.ToList();
        var costTotals = await LoadCostTotalsAsync(list.Select(d => d.Id));
        var showCost = ShowCostToCaller();
        return list.Select(d => MapToDietDto(d, costTotals, showCost)).ToList();
    }

    private async Task<DietDetailDto> MapDietDetailWithCostAsync(Diet diet)
    {
        var costTotals = await LoadCostTotalsAsync([diet.Id]);
        return MapToDietDetailDto(diet, costTotals, ShowCostToCaller());
    }

    /// <summary>
    /// Exact per-round-date consumption. Comparing raw timestamps would be wrong: CreatedAt carries a
    /// time of day while ScheduledDate is a midnight date, so a same-day programme's first round would
    /// otherwise count zero hives. The removal day counts as fed — RemovedOn is clamped to today
    /// server-side, so a hive removed the morning of a round it was on until then did receive it.
    /// Recomputed fresh on every read (never stored), which is exactly why removing a hive drops
    /// the *planned* total for the remaining future rounds immediately.
    /// </summary>
    private static (decimal? Planned, decimal? Consumed) ComputeConsumption(Diet d)
    {
        if (d.AmountPerHive is not decimal amount) return (null, null);

        decimal Fold(IEnumerable<FeedingEntry> rounds) => rounds.Sum(e =>
        {
            var date = e.ScheduledDate.Date;
            var hivesOn = d.Beehives.Count(db =>
                db.CreatedAt.Date <= date && (db.RemovedOn == null || db.RemovedOn.Value.Date >= date));
            return amount * hivesOn;
        });

        return (Fold(d.FeedingEntries), Fold(d.FeedingEntries.Where(e => e.Status == FeedingEntryStatus.Completed)));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates feeding rounds for a schedule starting at <paramref name="startDate"/>
    /// over <paramref name="durationDays"/> with one round every <paramref name="frequencyDays"/> days.
    /// </summary>
    private static List<FeedingEntry> GenerateEntries(DateTime startDate, int durationDays, int frequencyDays)
    {
        var entries = new List<FeedingEntry>();
        int count = durationDays / frequencyDays; // integer division
        if (count < 1) count = 1;

        for (int i = 0; i < count; i++)
        {
            entries.Add(new FeedingEntry
            {
                ScheduledDate = startDate.Date.AddDays(i * frequencyDays),
                Status        = FeedingEntryStatus.Pending,
            });
        }
        return entries;
    }

    /// <summary>
    /// Rebuilds pending feeding rounds after a diet update, preserving completed ones.
    /// </summary>
    private static void RecalculateEntries(Diet diet)
    {
        // Gather completed entry dates to avoid duplicating them
        var completedDates = diet.FeedingEntries
            .Where(e => e.Status == FeedingEntryStatus.Completed)
            .Select(e => e.ScheduledDate.Date)
            .ToHashSet();

        // Remove all pending entries (EF Core will handle DELETE via cascade)
        var pending = diet.FeedingEntries.Where(e => e.Status == FeedingEntryStatus.Pending).ToList();
        foreach (var p in pending)
            diet.FeedingEntries.Remove(p);

        // Generate the full schedule and add entries for dates not already completed
        int count = diet.DurationDays / diet.FrequencyDays;
        if (count < 1) count = 1;

        for (int i = 0; i < count; i++)
        {
            var date = diet.StartDate.Date.AddDays(i * diet.FrequencyDays);
            if (!completedDates.Contains(date))
            {
                diet.FeedingEntries.Add(new FeedingEntry
                {
                    ScheduledDate = date,
                    Status        = FeedingEntryStatus.Pending,
                    DietId        = diet.Id,
                });
            }
        }
    }

    private static DietStatus CalculateInitialStatus(DateTime startDate)
        => startDate.Date <= DateTime.UtcNow.Date
            ? DietStatus.InProgress
            : DietStatus.NotStarted;

    private static DietStatus CalculateStatus(Diet diet)
    {
        if (diet.Status == DietStatus.StoppedEarly) return DietStatus.StoppedEarly;

        var allCompleted = diet.FeedingEntries.Count > 0
                           && diet.FeedingEntries.All(e => e.Status == FeedingEntryStatus.Completed);

        if (allCompleted) return DietStatus.Completed;

        return diet.StartDate.Date <= DateTime.UtcNow.Date
            ? DietStatus.InProgress
            : DietStatus.NotStarted;
    }

    /// <summary>Earliest round still ahead of us; if the programme is behind, the oldest overdue one.</summary>
    private static DateTime? NextFeedingDate(IEnumerable<FeedingEntry> entries)
    {
        var pending = entries
            .Where(e => e.Status == FeedingEntryStatus.Pending)
            .OrderBy(e => e.ScheduledDate)
            .Select(e => e.ScheduledDate)
            .ToList();

        var today = DateTime.UtcNow.Date;
        foreach (var d in pending)
            if (d.Date >= today) return d;

        return pending.Count > 0 ? pending[0] : null;
    }

    // ── Mapping helpers (avoid AutoMapper for computed props) ─────────────────

    private static T MapCommon<T>(
        T dto, Diet d,
        IReadOnlyDictionary<int, List<(string Currency, decimal Total)>> costTotals,
        bool showCost) where T : DietDto
    {
        dto.Id                     = d.Id;
        dto.ApiaryId               = d.ApiaryId;
        dto.ApiaryName             = d.Apiary?.Name;
        dto.Name                   = d.Name;
        dto.StartDate              = d.StartDate;
        dto.Reason                 = d.Reason;
        dto.ReasonName             = BsLabels.Label(d.Reason);
        dto.CustomReason           = d.CustomReason;
        dto.DurationDays           = d.DurationDays;
        dto.FrequencyDays          = d.FrequencyDays;
        dto.FoodType               = d.FoodType;
        dto.FoodTypeName           = BsLabels.Label(d.FoodType);
        dto.CustomFoodType         = d.CustomFoodType;
        dto.AmountPerHive          = d.AmountPerHive;
        dto.AmountUnit             = d.AmountUnit;
        dto.AmountUnitName         = d.AmountUnit.HasValue ? BsLabels.Label(d.AmountUnit.Value) : null;
        dto.AmountNote             = d.AmountNote;
        dto.Status                 = d.Status;
        dto.StatusName             = BsLabels.Label(d.Status);
        dto.EarlyCompletionComment = d.EarlyCompletionComment;
        dto.HiveCount              = d.Beehives.Count(db => db.RemovedOn == null);
        dto.TotalEntries           = d.FeedingEntries.Count;
        dto.CompletedEntries       = d.FeedingEntries.Count(e => e.Status == FeedingEntryStatus.Completed);
        dto.NextFeedingDate        = NextFeedingDate(d.FeedingEntries);
        dto.CreatedByName          = d.CreatedBy != null ? $"{d.CreatedBy.FirstName} {d.CreatedBy.LastName}" : null;
        dto.CreatedAt              = d.CreatedAt;

        var (planned, consumed) = ComputeConsumption(d);
        dto.PlannedAmount  = planned;
        dto.ConsumedAmount = consumed;

        if (showCost)
        {
            var totals = costTotals.TryGetValue(d.Id, out var list) ? list : [];
            if (totals.Count > 0)
            {
                dto.CostTotals = totals.Select(t => new CostTotalDto { Currency = t.Currency, Total = t.Total }).ToList();
                if (totals.Count == 1 && dto.HiveCount > 0)
                    dto.CostPerHive = totals[0].Total / dto.HiveCount;
            }
        }
        // else: CostTotals/CostPerHive stay null — server-side omission for Beekeepers, not a
        // hidden-but-transmitted number.

        return dto;
    }

    private static DietDto MapToDietDto(
        Diet d, IReadOnlyDictionary<int, List<(string Currency, decimal Total)>> costTotals, bool showCost)
        => MapCommon(new DietDto(), d, costTotals, showCost);

    private static DietDetailDto MapToDietDetailDto(
        Diet d, IReadOnlyDictionary<int, List<(string Currency, decimal Total)>> costTotals, bool showCost)
    {
        var dto = MapCommon(new DietDetailDto(), d, costTotals, showCost);

        dto.FeedingEntries = d.FeedingEntries
            .OrderBy(e => e.ScheduledDate)
            .Select(MapToFeedingEntryDto)
            .ToList();

        // Active first, then the removal history — the detail page greys the latter out.
        dto.Beehives = d.Beehives
            .OrderBy(db => db.RemovedOn.HasValue)
            .ThenBy(db => db.Beehive != null ? db.Beehive.Name : string.Empty)
            .Select(db => new DietBeehiveDto
            {
                Id          = db.Id,
                BeehiveId   = db.BeehiveId,
                BeehiveName = db.Beehive?.Name ?? $"Košnica {db.BeehiveId}",
                AddedOn     = db.CreatedAt,
                RemovedOn   = db.RemovedOn,
            })
            .ToList();

        return dto;
    }

    private static FeedingEntryDto MapToFeedingEntryDto(FeedingEntry e) => new()
    {
        Id             = e.Id,
        ScheduledDate  = e.ScheduledDate,
        Status         = e.Status,
        StatusName     = BsLabels.Label(e.Status),
        CompletionDate = e.CompletionDate,
        Note           = e.Note,
        DietId         = e.DietId,
    };

    private static DietActiveInfoDto MapToActiveInfoDto(DietActiveInfo i) => new()
    {
        BeehiveId       = i.BeehiveId,
        DietId          = i.DietId,
        DietName        = i.DietName,
        FoodType        = i.FoodType,
        FoodTypeName    = BsLabels.Label(i.FoodType),
        CustomFoodType  = i.CustomFoodType,
        AmountPerHive   = i.AmountPerHive,
        AmountUnit      = i.AmountUnit,
        AmountUnitName  = i.AmountUnit.HasValue ? BsLabels.Label(i.AmountUnit.Value) : null,
        AmountNote      = i.AmountNote,
        StartDate       = i.StartDate,
        NextFeedingDate = i.NextFeedingDate,
        CompletedRounds = i.CompletedRounds,
        TotalRounds     = i.TotalRounds,
    };
}
