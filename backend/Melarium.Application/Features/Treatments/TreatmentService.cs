using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Treatments.DTOs;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Treatments;

/// <summary>
/// Treatment (veterinary medicine record) CRUD with apiary-scoped authorization identical to harvests:
/// managers write within scope; a Beekeeper has read-only access to treatments that contain at least one
/// of their assigned hives. Karenca/status are computed via <see cref="TreatmentStatusHelper"/>.
/// </summary>
public class TreatmentService : ITreatmentService
{
    private readonly IUnitOfWork _uow;
    private readonly IAccessGuard _access;
    private readonly ICurrentUser _currentUser;

    public TreatmentService(IUnitOfWork uow, IAccessGuard access, ICurrentUser currentUser)
    {
        _uow = uow;
        _access = access;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<TreatmentDto>> GetAllAsync(int? apiaryId, int? beehiveId, int? year)
    {
        if (_currentUser.Role == UserRole.Beekeeper)
            return await GetForBeekeeperAsync(apiaryId, beehiveId, year);

        if (beehiveId is int bid)
        {
            await _access.EnsureCanAccessBeehiveAsync(bid);
            return (await _uow.Treatments.GetByBeehiveAsync(bid))
                .Where(t => year == null || t.StartDate.Year == year)
                .Select(ToListDto);
        }

        if (apiaryId is int aid)
        {
            await _access.EnsureCanManageApiaryAsync(aid);
            return (await _uow.Treatments.GetByApiaryAsync(aid, year)).Select(ToListDto);
        }

        return _currentUser.Role switch
        {
            UserRole.ApiaryAdmin when _currentUser.ApiaryId is int myApiary =>
                (await _uow.Treatments.GetByApiaryAsync(myApiary, year)).Select(ToListDto),
            UserRole.OrganizationAdmin when _currentUser.OrganizationId is int orgId =>
                (await _uow.Treatments.GetByOrganizationAsync(orgId, year)).Select(ToListDto),
            _ => [], // SystemAdmin (no org) must pass a filter
        };
    }

    private async Task<IEnumerable<TreatmentDto>> GetForBeekeeperAsync(int? apiaryId, int? beehiveId, int? year)
    {
        var hiveIds = await _access.GetAssignedBeehiveIdsAsync();
        if (hiveIds.Count == 0) return [];

        var apiaryIds = await _access.GetAssignedApiaryIdsAsync();

        IEnumerable<Treatment> treatments;
        if (beehiveId is int bid)
        {
            if (!hiveIds.Contains(bid)) throw new ForbiddenAccessException();
            treatments = await _uow.Treatments.GetByBeehiveAsync(bid);
        }
        else if (apiaryId is int aid)
        {
            if (!apiaryIds.Contains(aid)) throw new ForbiddenAccessException();
            treatments = await _uow.Treatments.GetByApiaryAsync(aid, year);
        }
        else
        {
            var all = new List<Treatment>();
            foreach (var ap in apiaryIds)
                all.AddRange(await _uow.Treatments.GetByApiaryAsync(ap, year));
            treatments = all;
        }

        return treatments
            .Where(t => year == null || t.StartDate.Year == year)
            .Where(t => t.Entries.Any(e => hiveIds.Contains(e.BeehiveId)))
            .Select(ToListDto);
    }

    public async Task<TreatmentDetailDto> GetByIdAsync(int id)
    {
        var treatment = await _uow.Treatments.GetWithEntriesAsync(id)
            ?? throw new NotFoundException(nameof(Treatment), id);

        await EnsureCanReadAsync(treatment);
        return ToDetailDto(treatment);
    }

    public async Task<TreatmentDetailDto> CreateAsync(CreateTreatmentDto dto)
    {
        await _access.EnsureCanManageApiaryAsync(dto.ApiaryId);
        await EnsureEntriesBelongToApiaryAsync(dto.ApiaryId, dto.Entries.Select(e => e.BeehiveId));

        var treatment = new Treatment
        {
            ApiaryId        = dto.ApiaryId,
            Purpose         = dto.Purpose,
            ProductName     = dto.ProductName.Trim(),
            ActiveSubstance = dto.ActiveSubstance,
            Method          = dto.Method,
            DosePerHive     = dto.DosePerHive.Trim(),
            StartDate       = dto.StartDate,
            EndDate         = dto.EndDate,
            WithdrawalDays  = dto.WithdrawalDays,
            TotalRounds     = dto.TotalRounds,
            IntervalDays    = dto.IntervalDays,
            BatchNumber     = dto.BatchNumber,
            Supplier        = dto.Supplier,
            Notes           = dto.Notes,
            CreatedById     = _currentUser.UserId,
            Entries         = dto.Entries.Select(ToEntity).ToList(),
        };

        treatment.Rounds = GenerateRounds(treatment.StartDate, dto.TotalRounds, dto.IntervalDays);

        await _uow.Treatments.AddAsync(treatment);
        await _uow.SaveChangesAsync();

        var created = await _uow.Treatments.GetWithEntriesAsync(treatment.Id)
            ?? throw new InvalidOperationException("Treatment was not saved correctly.");
        return ToDetailDto(created);
    }

    public async Task<TreatmentDetailDto> UpdateAsync(int id, UpdateTreatmentDto dto)
    {
        var treatment = await _uow.Treatments.GetWithEntriesAsync(id)
            ?? throw new NotFoundException(nameof(Treatment), id);

        await _access.EnsureCanManageApiaryAsync(treatment.ApiaryId);
        await EnsureEntriesBelongToApiaryAsync(treatment.ApiaryId, dto.Entries.Select(e => e.BeehiveId));

        treatment.Purpose         = dto.Purpose;
        treatment.ProductName     = dto.ProductName.Trim();
        treatment.ActiveSubstance = dto.ActiveSubstance;
        treatment.Method          = dto.Method;
        treatment.DosePerHive     = dto.DosePerHive.Trim();
        treatment.StartDate       = dto.StartDate;
        treatment.EndDate         = dto.EndDate;
        treatment.WithdrawalDays  = dto.WithdrawalDays;
        treatment.TotalRounds     = dto.TotalRounds;
        treatment.IntervalDays    = dto.IntervalDays;
        treatment.BatchNumber     = dto.BatchNumber;
        treatment.Supplier        = dto.Supplier;
        treatment.Notes           = dto.Notes;

        treatment.Entries.Clear();
        foreach (var e in dto.Entries)
            treatment.Entries.Add(ToEntity(e));

        // Recalculate rounds: keep completed, replace all pending
        RecalculateRounds(treatment);

        treatment.UpdatedAt = DateTime.UtcNow;

        await _uow.Treatments.UpdateAsync(treatment);
        await _uow.SaveChangesAsync();

        var updated = await _uow.Treatments.GetWithEntriesAsync(id);
        return ToDetailDto(updated!);
    }

    public async Task DeleteAsync(int id)
    {
        var treatment = await _uow.Treatments.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Treatment), id);

        await _access.EnsureCanManageApiaryAsync(treatment.ApiaryId);

        await _uow.Treatments.DeleteAsync(treatment);
        await _uow.SaveChangesAsync();
    }

    public async Task<TreatmentRoundDto> CompleteTreatmentRoundAsync(int treatmentId, int roundId, CompleteTreatmentRoundDto dto)
    {
        var treatment = await _uow.Treatments.GetWithEntriesAsync(treatmentId)
            ?? throw new NotFoundException(nameof(Treatment), treatmentId);

        // Same rule as reading — a Beekeeper who may see the treatment physically applies it in the
        // field, so they may tick its rounds too (mirrors Diet's feeding-round rule).
        await EnsureCanReadAsync(treatment);

        var round = treatment.Rounds.FirstOrDefault(r => r.Id == roundId)
            ?? throw new NotFoundException(nameof(TreatmentRound), roundId);

        if (round.Status == TreatmentRoundStatus.Completed)
            throw new BusinessRuleException("This treatment round is already completed.");

        round.Status         = TreatmentRoundStatus.Completed;
        round.CompletionDate = DateTime.UtcNow;
        round.Note           = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        round.UpdatedAt      = DateTime.UtcNow;

        await _uow.Treatments.UpdateAsync(treatment);
        await _uow.SaveChangesAsync();

        return MapToRoundDto(round);
    }

    // ── Authorization helpers ────────────────────────────────────────────────────

    private async Task EnsureCanReadAsync(Treatment treatment)
    {
        if (_currentUser.Role == UserRole.Beekeeper)
        {
            var hiveIds = await _access.GetAssignedBeehiveIdsAsync();
            if (!treatment.Entries.Any(e => hiveIds.Contains(e.BeehiveId)))
                throw new ForbiddenAccessException();
            return;
        }

        await _access.EnsureCanManageApiaryAsync(treatment.ApiaryId);
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

    // ── Mapping ──────────────────────────────────────────────────────────────────

    private static TreatmentEntry ToEntity(CreateTreatmentEntryDto e) => new()
    {
        BeehiveId = e.BeehiveId,
        DoseNote  = string.IsNullOrWhiteSpace(e.DoseNote) ? null : e.DoseNote.Trim(),
    };

    /// <summary>
    /// Generates <paramref name="totalRounds"/> application rounds, one every <paramref name="intervalDays"/>
    /// days starting at <paramref name="startDate"/>. <paramref name="totalRounds"/> == 1 (the common case —
    /// strips, trickling) always produces exactly one round on the start date, whatever the interval.
    /// </summary>
    private static List<TreatmentRound> GenerateRounds(DateTime startDate, int totalRounds, int intervalDays)
    {
        var rounds = new List<TreatmentRound>();
        var count = totalRounds < 1 ? 1 : totalRounds;

        for (int i = 0; i < count; i++)
        {
            rounds.Add(new TreatmentRound
            {
                ScheduledDate = startDate.Date.AddDays(i * intervalDays),
                Status        = TreatmentRoundStatus.Pending,
            });
        }
        return rounds;
    }

    /// <summary>Rebuilds pending rounds after a treatment update, preserving completed ones.</summary>
    private static void RecalculateRounds(Treatment treatment)
    {
        var completedDates = treatment.Rounds
            .Where(r => r.Status == TreatmentRoundStatus.Completed)
            .Select(r => r.ScheduledDate.Date)
            .ToHashSet();

        var pending = treatment.Rounds.Where(r => r.Status == TreatmentRoundStatus.Pending).ToList();
        foreach (var p in pending)
            treatment.Rounds.Remove(p);

        var count = treatment.TotalRounds < 1 ? 1 : treatment.TotalRounds;
        for (int i = 0; i < count; i++)
        {
            var date = treatment.StartDate.Date.AddDays(i * treatment.IntervalDays);
            if (!completedDates.Contains(date))
            {
                treatment.Rounds.Add(new TreatmentRound
                {
                    ScheduledDate = date,
                    Status        = TreatmentRoundStatus.Pending,
                    TreatmentId   = treatment.Id,
                });
            }
        }
    }

    private static TreatmentRoundDto MapToRoundDto(TreatmentRound r) => new()
    {
        Id             = r.Id,
        ScheduledDate  = r.ScheduledDate,
        Status         = r.Status,
        StatusName     = BsLabels.Label(r.Status),
        CompletionDate = r.CompletionDate,
        Note           = r.Note,
        TreatmentId    = r.TreatmentId,
    };

    private static T MapCommon<T>(T dto, Treatment t) where T : TreatmentDto
    {
        var status = TreatmentStatusHelper.Status(t.StartDate, t.EndDate, t.WithdrawalDays, DateTime.UtcNow);

        dto.Id                  = t.Id;
        dto.ApiaryId            = t.ApiaryId;
        dto.ApiaryName          = t.Apiary?.Name;
        dto.Purpose             = t.Purpose;
        dto.PurposeName         = BsLabels.Label(t.Purpose);
        dto.ProductName         = t.ProductName;
        dto.ActiveSubstance     = t.ActiveSubstance;
        dto.ActiveSubstanceName = BsLabels.Label(t.ActiveSubstance);
        dto.Method              = t.Method;
        dto.MethodName          = BsLabels.Label(t.Method);
        dto.DosePerHive         = t.DosePerHive;
        dto.StartDate           = t.StartDate;
        dto.EndDate             = t.EndDate;
        dto.WithdrawalDays      = t.WithdrawalDays;
        dto.TotalRounds         = t.TotalRounds;
        dto.IntervalDays        = t.IntervalDays;
        dto.CompletedRounds     = t.Rounds.Count(r => r.Status == TreatmentRoundStatus.Completed);
        dto.BatchNumber         = t.BatchNumber;
        dto.Supplier            = t.Supplier;
        dto.Notes               = t.Notes;
        dto.KarencaUntil        = TreatmentStatusHelper.KarencaUntil(t.StartDate, t.EndDate, t.WithdrawalDays);
        dto.Status              = status;
        dto.StatusName          = BsLabels.Label(status);
        dto.HiveCount           = t.Entries.Count;
        dto.HiveNames           = t.Entries.Where(e => e.Beehive is not null).Select(e => e.Beehive!.Name).OrderBy(n => n).ToList();
        dto.CreatedByName       = t.CreatedBy is not null ? $"{t.CreatedBy.FirstName} {t.CreatedBy.LastName}" : null;
        dto.CreatedAt           = t.CreatedAt;
        return dto;
    }

    private static TreatmentDto ToListDto(Treatment t) => MapCommon(new TreatmentDto(), t);

    private static TreatmentDetailDto ToDetailDto(Treatment t)
    {
        var dto = MapCommon(new TreatmentDetailDto(), t);
        dto.Entries = t.Entries
            .OrderBy(e => e.Beehive != null ? e.Beehive.Name : string.Empty)
            .Select(e => new TreatmentEntryDto
            {
                Id          = e.Id,
                BeehiveId   = e.BeehiveId,
                BeehiveName = e.Beehive?.Name,
                DoseNote    = e.DoseNote,
            })
            .ToList();
        dto.Rounds = t.Rounds
            .OrderBy(r => r.ScheduledDate)
            .Select(MapToRoundDto)
            .ToList();
        return dto;
    }
}
