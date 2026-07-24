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
            BatchNumber     = dto.BatchNumber,
            Supplier        = dto.Supplier,
            Notes           = dto.Notes,
            CreatedById     = _currentUser.UserId,
            Entries         = dto.Entries.Select(ToEntity).ToList(),
        };

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
        treatment.BatchNumber     = dto.BatchNumber;
        treatment.Supplier        = dto.Supplier;
        treatment.Notes           = dto.Notes;

        treatment.Entries.Clear();
        foreach (var e in dto.Entries)
            treatment.Entries.Add(ToEntity(e));

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
        return dto;
    }
}
