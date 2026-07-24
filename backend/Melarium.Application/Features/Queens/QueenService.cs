using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Queens.DTOs;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Queens;

public class QueenService : IQueenService
{
    private readonly IUnitOfWork _uow;
    private readonly IAccessGuard _access;
    private readonly ICurrentUser _currentUser;

    public QueenService(IUnitOfWork uow, IAccessGuard access, ICurrentUser currentUser)
    {
        _uow = uow;
        _access = access;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<QueenDto>> GetByBeehiveIdAsync(int beehiveId)
    {
        if (!await _uow.Beehives.ExistsAsync(beehiveId))
            throw new NotFoundException(nameof(Beehive), beehiveId);

        await _access.EnsureCanAccessBeehiveAsync(beehiveId);

        var queens = await _uow.Queens.GetByBeehiveIdAsync(beehiveId);
        return queens.Select(ToDto);
    }

    public async Task<QueenDto> CreateAsync(int beehiveId, CreateQueenDto dto)
    {
        if (!await _uow.Beehives.ExistsAsync(beehiveId))
            throw new NotFoundException(nameof(Beehive), beehiveId);

        await _access.EnsureCanAccessBeehiveAsync(beehiveId);

        var queen = new Queen
        {
            BeehiveId      = beehiveId,
            Year           = dto.Year,
            MarkColor      = dto.MarkColor ?? QueenMarkColorHelper.ForYear(dto.Year),
            IsMarked       = dto.IsMarked,
            IsClipped      = dto.IsClipped,
            Origin         = dto.Origin,
            Status         = QueenStatus.Active,
            IntroducedDate = dto.IntroducedDate,
            Notes          = dto.Notes,
        };

        // Replacing: the previous active queen is closed in the same SaveChanges (atomic).
        var current = await _uow.Queens.GetActiveByBeehiveIdAsync(beehiveId);
        if (current is not null)
        {
            current.Status  = QueenStatus.Replaced;
            current.EndDate = dto.IntroducedDate;
            await _uow.Queens.UpdateAsync(current);
        }

        await _uow.Queens.AddAsync(queen);
        await _uow.SaveChangesAsync();

        return ToDto(queen);
    }

    public async Task<QueenDto> UpdateAsync(int id, UpdateQueenDto dto)
    {
        var queen = await _uow.Queens.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Queen), id);

        await _access.EnsureCanAccessBeehiveAsync(queen.BeehiveId);

        if (dto.Status == QueenStatus.Active)
        {
            var active = await _uow.Queens.GetActiveByBeehiveIdAsync(queen.BeehiveId);
            if (active is not null && active.Id != queen.Id)
                throw new BusinessRuleException("The beehive already has an active queen — close it first.");
        }

        DateTime? newEndDate = dto.Status == QueenStatus.Active
            ? null
            : dto.EndDate ?? queen.EndDate ?? DateTime.UtcNow;

        // Snapshot field-level changes before mutating, so mistakes in the initial data
        // (and every later correction) stay traceable in the queen's edit history.
        var edits = new List<QueenEditLog>();
        void TrackChange(string label, string? oldValue, string? newValue)
        {
            if (oldValue == newValue) return;
            edits.Add(new QueenEditLog
            {
                QueenId      = queen.Id,
                EditedById   = _currentUser.UserId,
                FieldLabel   = label,
                OldValue     = oldValue,
                NewValue     = newValue,
            });
        }

        TrackChange("Godište", queen.Year.ToString(), dto.Year.ToString());
        TrackChange("Boja oznake", BsLabels.Label(queen.MarkColor), BsLabels.Label(dto.MarkColor));
        TrackChange("Označena", FormatBool(queen.IsMarked), FormatBool(dto.IsMarked));
        TrackChange("Podrezana krila", FormatBool(queen.IsClipped), FormatBool(dto.IsClipped));
        TrackChange("Porijeklo", BsLabels.Label(queen.Origin), BsLabels.Label(dto.Origin));
        TrackChange("Status", BsLabels.Label(queen.Status), BsLabels.Label(dto.Status));
        TrackChange("U košnici od", FormatDate(queen.IntroducedDate), FormatDate(dto.IntroducedDate));
        TrackChange("Do datuma", FormatDate(queen.EndDate), FormatDate(newEndDate));
        TrackChange("Napomene", queen.Notes, dto.Notes);

        queen.Year           = dto.Year;
        queen.MarkColor      = dto.MarkColor;
        queen.IsMarked       = dto.IsMarked;
        queen.IsClipped      = dto.IsClipped;
        queen.Origin         = dto.Origin;
        queen.Status         = dto.Status;
        queen.IntroducedDate = dto.IntroducedDate;
        queen.Notes          = dto.Notes;
        queen.EndDate        = newEndDate;

        await _uow.Queens.UpdateAsync(queen);
        foreach (var edit in edits)
            await _uow.QueenEditLogs.AddAsync(edit);
        await _uow.SaveChangesAsync();

        return ToDto(queen);
    }

    public async Task<IEnumerable<QueenEditLogDto>> GetEditHistoryAsync(int queenId)
    {
        var queen = await _uow.Queens.GetByIdAsync(queenId)
            ?? throw new NotFoundException(nameof(Queen), queenId);

        await _access.EnsureCanAccessBeehiveAsync(queen.BeehiveId);

        var logs = await _uow.QueenEditLogs.GetByQueenIdAsync(queenId);
        return logs.Select(l => new QueenEditLogDto
        {
            Id           = l.Id,
            FieldLabel   = l.FieldLabel,
            OldValue     = l.OldValue,
            NewValue     = l.NewValue,
            EditedAt     = l.CreatedAt,
            EditedByName = l.EditedBy is null ? null : $"{l.EditedBy.FirstName} {l.EditedBy.LastName}".Trim(),
        });
    }

    private static string FormatBool(bool value) => value ? "Da" : "Ne";

    private static string? FormatDate(DateTime? value) => value?.ToString("dd.MM.yyyy");

    public async Task DeleteAsync(int id)
    {
        var queen = await _uow.Queens.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Queen), id);

        await _access.EnsureCanAccessBeehiveAsync(queen.BeehiveId);

        await _uow.Queens.DeleteAsync(queen);
        await _uow.SaveChangesAsync();
    }

    // Manual mapping — the DTO carries computed Bosnian *Name fields (same policy as Diets/Admin).
    private static QueenDto ToDto(Queen q) => new()
    {
        Id             = q.Id,
        Year           = q.Year,
        MarkColor      = q.MarkColor,
        MarkColorName  = BsLabels.Label(q.MarkColor),
        IsMarked       = q.IsMarked,
        IsClipped      = q.IsClipped,
        Origin         = q.Origin,
        OriginName     = BsLabels.Label(q.Origin),
        Status         = q.Status,
        StatusName     = BsLabels.Label(q.Status),
        IntroducedDate = q.IntroducedDate,
        EndDate        = q.EndDate,
        Notes          = q.Notes,
        BeehiveId      = q.BeehiveId,
        CreatedAt      = q.CreatedAt,
    };
}
