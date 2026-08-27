using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.BeehiveMerges.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Melarium.Application.Features.BeehiveMerges;

/// <summary>
/// Sastavljanje društava (SPEC-19).
///
/// <para><b>Why this writes through <c>_uow</c> rather than calling <c>DietService</c> and
/// <c>TodoService</c>.</b> SPEC-19 §3 requires the whole merge to be one <c>SaveChanges</c>, so a
/// failure half-way leaves nothing behind — a hive flagged as merged whose queen is still active is
/// exactly the corruption the undo journal cannot repair. Those services each commit internally, so
/// calling them would produce several commits. Nothing is lost by going direct: unlike SPEC-17's AI
/// executor, there is no guard, plan limit or notification cascade hiding behind
/// <c>DietService.RemoveBeehiveAsync</c> — it sets one column — and this service performs the
/// stricter access check (both apiaries) itself. The semantics are copied exactly, including
/// <c>RemovedOn = today</c>.</para>
/// </summary>
public class BeehiveMergeService : IBeehiveMergeService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessGuard _access;
    private readonly INotificationService _notifications;
    private readonly ILogger<BeehiveMergeService> _logger;

    public BeehiveMergeService(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IAccessGuard access,
        INotificationService notifications,
        ILogger<BeehiveMergeService> logger)
    {
        _uow           = uow;
        _currentUser   = currentUser;
        _access        = access;
        _notifications = notifications;
        _logger        = logger;
    }

    // ── Merge ──────────────────────────────────────────────────────────────────

    public async Task<BeehiveMergeDto> MergeAsync(CreateBeehiveMergeDto dto)
    {
        var (source, target) = await LoadAndAuthorizePairAsync(dto.SourceBeehiveId, dto.TargetBeehiveId);

        if (dto.MergedAt.Date > DateTime.UtcNow.Date.AddDays(1))
            throw new BusinessRuleException("Datum sastavljanja ne može biti u budućnosti.");

        var sourceQueen = await _uow.Queens.GetActiveByBeehiveIdAsync(source.Id);
        var targetQueen = await _uow.Queens.GetActiveByBeehiveIdAsync(target.Id);

        if (dto.QueenOutcome == MergeQueenOutcome.KeptSource && sourceQueen is null)
            throw new BusinessRuleException(
                $"Košnica '{source.Name}' nema aktivnu maticu, pa ona ne može ostati nakon sastavljanja.");

        var mergedAt = dto.MergedAt.Date;

        var merge = new BeehiveMerge
        {
            SourceBeehiveId = source.Id,
            TargetBeehiveId = target.Id,
            MergedAt        = mergedAt,
            Reason          = dto.Reason,
            Method          = dto.Method,
            QueenOutcome    = dto.QueenOutcome,
            Notes           = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedById     = _currentUser.UserId,
        };
        await _uow.BeehiveMerges.AddAsync(merge);

        var journalQueens     = await ApplyQueenOutcomeAsync(dto.QueenOutcome, source, target, sourceQueen, targetQueen, mergedAt);
        var journalTodos      = await DeleteOpenTodosAsync(source.Id);
        var journalDietHives  = await RemoveFromDietsAsync(source, target, mergedAt);
        var journalTreatments = await AnnotateOngoingTreatmentsAsync(source, target, mergedAt);

        source.MergedIntoBeehiveId = target.Id;
        source.MergedAt            = mergedAt;
        source.UpdatedAt           = DateTime.UtcNow;
        await _uow.Beehives.UpdateAsync(source);

        merge.UndoJournalJson = new MergeUndoJournal(
            journalQueens, journalTodos, journalDietHives, journalTreatments).ToJson();

        await _uow.SaveChangesAsync();

        // After the commit, exactly like BeehiveService.CreateAsync — NotificationService saves on its
        // own, and a notification failure must never undo a merge that already happened.
        await SendMergedNotificationsAsync(source, target);

        return await LoadDtoAsync(merge.Id);
    }

    /// <summary>
    /// SPEC-19 §3.2 step 2. Order matters for <see cref="MergeQueenOutcome.KeptSource"/>: the target's
    /// queen is closed <b>before</b> the source's queen moves in, otherwise the hive briefly holds two
    /// active queens — the state <c>QueenService.UpdateAsync</c> refuses outright.
    /// </summary>
    private async Task<List<JournalQueen>> ApplyQueenOutcomeAsync(
        MergeQueenOutcome outcome, Beehive source, Beehive target, Queen? sourceQueen, Queen? targetQueen, DateTime mergedAt)
    {
        var journal = new List<JournalQueen>();

        async Task RemoveAsync(Queen queen)
        {
            journal.Add(new JournalQueen(queen.Id, queen.BeehiveId, queen.Status, queen.EndDate, queen.Notes));

            queen.Status    = QueenStatus.Removed;
            queen.EndDate   = mergedAt;
            queen.Notes     = AppendLine(queen.Notes,
                $"Uklonjena pri sastavljanju društva (košnica {source.Name} → košnica {target.Name}, {mergedAt:dd.MM.yyyy}.).");
            queen.UpdatedAt = DateTime.UtcNow;
            await _uow.Queens.UpdateAsync(queen);
        }

        switch (outcome)
        {
            case MergeQueenOutcome.KeptTarget:
                if (sourceQueen is not null) await RemoveAsync(sourceQueen);
                break;

            case MergeQueenOutcome.KeptSource:
                if (targetQueen is not null) await RemoveAsync(targetQueen);

                if (sourceQueen is not null)
                {
                    journal.Add(new JournalQueen(
                        sourceQueen.Id, sourceQueen.BeehiveId, sourceQueen.Status, sourceQueen.EndDate, sourceQueen.Notes));

                    sourceQueen.BeehiveId = target.Id;
                    sourceQueen.Notes     = AppendLine(sourceQueen.Notes,
                        $"Prešla iz košnice {source.Name} pri sastavljanju društva {mergedAt:dd.MM.yyyy}.");
                    sourceQueen.UpdatedAt = DateTime.UtcNow;
                    await _uow.Queens.UpdateAsync(sourceQueen);
                }
                break;

            case MergeQueenOutcome.None:
                if (sourceQueen is not null) await RemoveAsync(sourceQueen);
                if (targetQueen is not null) await RemoveAsync(targetQueen);
                break;
        }

        return journal;
    }

    /// <summary>
    /// SPEC-19 §3.2 step 3 (D3). Only open, beehive-scoped todos: a completed one is history, and an
    /// apiary-level todo has nothing to do with this hive.
    /// </summary>
    private async Task<List<JournalTodo>> DeleteOpenTodosAsync(int sourceBeehiveId)
    {
        var open = (await _uow.Todos.GetByBeehiveIdAsync(sourceBeehiveId))
            .Where(t => !t.IsCompleted)
            .ToList();

        var journal = new List<JournalTodo>();
        foreach (var todo in open)
        {
            journal.Add(new JournalTodo(
                todo.Title, todo.Notes, todo.DueDate, todo.Priority,
                todo.AssignedToId, todo.CreatedById, todo.CreatedAt));

            await _uow.Todos.DeleteAsync(todo);
        }

        return journal;
    }

    /// <summary>
    /// SPEC-19 §3.2 step 4 (D3). Since SPEC-12 a diet is an <b>apiary-level programme</b> covering a set
    /// of hives, so this takes the hive off the programme — it never stops the programme, which would
    /// end the feeding for every other hive on it. Only when this was the last hive still on it does
    /// the programme itself stop early, with a comment.
    /// </summary>
    private async Task<List<JournalDietHive>> RemoveFromDietsAsync(Beehive source, Beehive target, DateTime mergedAt)
    {
        var candidateIds = (await _uow.Diets.GetByBeehiveAsync(source.Id))
            .Where(d => d.Status is not (DietStatus.Completed or DietStatus.StoppedEarly))
            .Where(d => d.Beehives.Any(db => db.BeehiveId == source.Id && db.RemovedOn == null))
            .Select(d => d.Id)
            .ToList();

        var journal = new List<JournalDietHive>();

        foreach (var dietId in candidateIds)
        {
            var diet = await _uow.Diets.GetWithEntriesAsync(dietId);
            if (diet is null) continue;

            var link = diet.Beehives.FirstOrDefault(db => db.BeehiveId == source.Id && db.RemovedOn == null);
            if (link is null) continue;

            // Same value DietService.RemoveBeehiveAsync writes — today, never a client-supplied date.
            link.RemovedOn = DateTime.UtcNow.Date;
            link.UpdatedAt = DateTime.UtcNow;

            var previousStatus  = diet.Status;
            var previousComment = diet.EarlyCompletionComment;
            var lastOne = !diet.Beehives.Any(db => db.RemovedOn == null);

            if (lastOne)
            {
                diet.Status                 = DietStatus.StoppedEarly;
                diet.EarlyCompletionComment = $"Društvo sastavljeno s košnicom {target.Name} ({mergedAt:dd.MM.yyyy}.).";
            }

            diet.UpdatedAt = DateTime.UtcNow;
            await _uow.Diets.UpdateAsync(diet);

            journal.Add(new JournalDietHive(link.Id, diet.Id, lastOne, previousStatus, previousComment));
        }

        return journal;
    }

    /// <summary>
    /// SPEC-19 §3.2 step 5 (D3). The <see cref="TreatmentEntry"/> row is never deleted — it is the
    /// legally-retained medicine record (5 years). Only a note is appended, and <c>DoseNote</c> is not
    /// printed in the PDF register, so the legal artifact is unchanged.
    /// </summary>
    private async Task<List<JournalTreatmentEntry>> AnnotateOngoingTreatmentsAsync(Beehive source, Beehive target, DateTime mergedAt)
    {
        var ongoingIds = (await _uow.Treatments.GetByBeehiveAsync(source.Id))
            .Where(t => t.EndDate is null)
            .Select(t => t.Id)
            .ToList();

        var journal = new List<JournalTreatmentEntry>();

        foreach (var treatmentId in ongoingIds)
        {
            var treatment = await _uow.Treatments.GetWithEntriesAsync(treatmentId);
            var entry = treatment?.Entries.FirstOrDefault(e => e.BeehiveId == source.Id);
            if (treatment is null || entry is null) continue;

            journal.Add(new JournalTreatmentEntry(treatment.Id, entry.Id, entry.DoseNote));

            entry.DoseNote = AppendLine(entry.DoseNote,
                $"Prekinuto {mergedAt:dd.MM.yyyy}. — društvo sastavljeno s košnicom {target.Name}.");
            entry.UpdatedAt = DateTime.UtcNow;

            await _uow.Treatments.UpdateAsync(treatment);
        }

        return journal;
    }

    // ── Undo (SPEC-19 §4) ──────────────────────────────────────────────────────

    public async Task<BeehiveMergeDto> UndoAsync(int mergeId)
    {
        var merge = await _uow.BeehiveMerges.GetWithHivesAsync(mergeId)
            ?? throw new NotFoundException(nameof(BeehiveMerge), mergeId);

        if (merge.UndoneAt is not null)
            throw new BusinessRuleException("Ovo sastavljanje je već poništeno.");

        await LoadAndAuthorizePairAsync(merge.SourceBeehiveId, merge.TargetBeehiveId, skipMergedChecks: true);

        var now = DateTime.UtcNow;
        if (MergeUndoPolicy.DeadlineFor(merge, now) is null)
        {
            var elapsed = now - merge.CreatedAt;
            throw new BusinessRuleException(
                $"Rok za poništavanje je istekao — sastavljanje je zabilježeno prije {(int)elapsed.TotalHours} sati, " +
                "a poništiti se može samo unutar 24 sata.");
        }

        var journal = MergeUndoJournal.FromJson(merge.UndoJournalJson);
        if (journal is null)
            throw new BusinessRuleException(
                "Ovo sastavljanje nema zapis prethodnog stanja, pa se ne može automatski poništiti.");

        // Reverse order of §3.2, so nothing is restored on top of something not yet undone.
        await RestoreTreatmentsAsync(journal.Treatments);
        await RestoreDietsAsync(journal.DietHives);
        await RestoreTodosAsync(merge.SourceBeehiveId, journal.Todos);
        await RestoreQueensAsync(journal.Queens);

        var source = await _uow.Beehives.GetByIdAsync(merge.SourceBeehiveId)
            ?? throw new NotFoundException(nameof(Beehive), merge.SourceBeehiveId);
        source.MergedIntoBeehiveId = null;
        source.MergedAt            = null;
        source.UpdatedAt           = now;
        await _uow.Beehives.UpdateAsync(source);

        merge.UndoneAt   = now;
        merge.UndoneById = _currentUser.UserId;
        merge.UpdatedAt  = now;
        await _uow.BeehiveMerges.UpdateAsync(merge);

        await _uow.SaveChangesAsync();

        return await LoadDtoAsync(merge.Id);
    }

    private async Task RestoreTreatmentsAsync(IReadOnlyList<JournalTreatmentEntry> entries)
    {
        foreach (var snapshot in entries)
        {
            var treatment = await _uow.Treatments.GetWithEntriesAsync(snapshot.TreatmentId);
            var entry = treatment?.Entries.FirstOrDefault(e => e.Id == snapshot.TreatmentEntryId);
            if (treatment is null || entry is null) continue;

            entry.DoseNote  = snapshot.DoseNote;
            entry.UpdatedAt = DateTime.UtcNow;
            await _uow.Treatments.UpdateAsync(treatment);
        }
    }

    /// <summary>
    /// Clears <c>RemovedOn</c> on the <b>same</b> row rather than adding a new membership: a new row
    /// would carry a new <c>CreatedAt</c>, which is "when the hive joined the programme" and feeds the
    /// consumption maths (see the note on <see cref="DietBeehive"/>).
    /// </summary>
    private async Task RestoreDietsAsync(IReadOnlyList<JournalDietHive> dietHives)
    {
        foreach (var snapshot in dietHives)
        {
            var diet = await _uow.Diets.GetWithEntriesAsync(snapshot.DietId);
            var link = diet?.Beehives.FirstOrDefault(db => db.Id == snapshot.DietBeehiveId);
            if (diet is null || link is null) continue;

            link.RemovedOn = null;
            link.UpdatedAt = DateTime.UtcNow;

            if (snapshot.CompletedEarly)
            {
                diet.Status                 = snapshot.PreviousStatus;
                diet.EarlyCompletionComment = snapshot.PreviousEarlyCompletionComment;
            }

            diet.UpdatedAt = DateTime.UtcNow;
            await _uow.Diets.UpdateAsync(diet);
        }
    }

    private async Task RestoreTodosAsync(int beehiveId, IReadOnlyList<JournalTodo> todos)
    {
        foreach (var snapshot in todos)
        {
            await _uow.Todos.AddAsync(new Todo
            {
                Title        = snapshot.Title,
                Notes        = snapshot.Notes,
                DueDate      = snapshot.DueDate,
                Priority     = snapshot.Priority,
                IsCompleted  = false,
                AssignedToId = snapshot.AssignedToId,
                CreatedById  = snapshot.CreatedById,
                CreatedAt    = snapshot.CreatedAt,
                BeehiveId    = beehiveId,
            });
        }
    }

    private async Task RestoreQueensAsync(IReadOnlyList<JournalQueen> queens)
    {
        foreach (var snapshot in queens)
        {
            var queen = await _uow.Queens.GetByIdAsync(snapshot.Id);
            if (queen is null) continue;

            queen.BeehiveId = snapshot.BeehiveId;
            queen.Status    = snapshot.Status;
            queen.EndDate   = snapshot.EndDate;
            queen.Notes     = snapshot.Notes;
            queen.UpdatedAt = DateTime.UtcNow;
            await _uow.Queens.UpdateAsync(queen);
        }
    }

    // ── Reads ──────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<BeehiveMergeDto>> GetReceivedByBeehiveAsync(int beehiveId)
    {
        await _access.EnsureCanAccessBeehiveAsync(beehiveId);

        var merges = await _uow.BeehiveMerges.GetReceivedByBeehiveAsync(beehiveId);
        var now = DateTime.UtcNow;
        return merges.Select(m => ToDto(m, now)).ToList();
    }

    public async Task<MergePreviewDto> GetPreviewAsync(int sourceBeehiveId, int? targetBeehiveId)
    {
        var source = await _uow.Beehives.GetByIdAsync(sourceBeehiveId)
            ?? throw new NotFoundException(nameof(Beehive), sourceBeehiveId);

        await _access.EnsureCanManageApiaryAsync(source.ApiaryId);

        var apiary = await _uow.Apiaries.GetByIdAsync(source.ApiaryId);

        var openTodos = (await _uow.Todos.GetByBeehiveIdAsync(sourceBeehiveId)).Count(t => !t.IsCompleted);

        var diets = (await _uow.Diets.GetByBeehiveAsync(sourceBeehiveId))
            .Where(d => d.Status is not (DietStatus.Completed or DietStatus.StoppedEarly))
            .Where(d => d.Beehives.Any(db => db.BeehiveId == sourceBeehiveId && db.RemovedOn == null))
            .Select(d => d.Name)
            .ToList();

        var treatments = (await _uow.Treatments.GetByBeehiveAsync(sourceBeehiveId)).ToList();

        var sourceQueen = await _uow.Queens.GetActiveByBeehiveIdAsync(sourceBeehiveId);
        Queen? targetQueen = null;
        if (targetBeehiveId is int targetId && targetId != sourceBeehiveId)
        {
            var target = await _uow.Beehives.GetByIdAsync(targetId);
            if (target is not null && await _access.CanAccessBeehiveAsync(targetId))
                targetQueen = await _uow.Queens.GetActiveByBeehiveIdAsync(targetId);
        }

        // The latest treatment still inside its withdrawal window: the bees carry the karenca with
        // them into the receiving hive. Reported, never blocking (§3.1).
        var today = DateTime.UtcNow;
        var inKarenca = treatments
            .Where(t => TreatmentStatusHelper.Status(t.StartDate, t.EndDate, t.WithdrawalDays, today)
                        is TreatmentStatus.InProgress or TreatmentStatus.Karenca)
            .Where(t => t.WithdrawalDays > 0)
            .OrderByDescending(t => TreatmentStatusHelper.KarencaUntil(t.StartDate, t.EndDate, t.WithdrawalDays))
            .FirstOrDefault();

        return new MergePreviewDto
        {
            BeehiveId             = source.Id,
            BeehiveName           = source.Name,
            ApiaryName            = apiary?.Name ?? string.Empty,
            OpenTodoCount         = openTodos,
            ActiveDietNames       = diets,
            OngoingTreatmentNames = treatments.Where(t => t.EndDate is null).Select(t => t.ProductName).ToList(),
            SourceQueenSummary    = DescribeQueen(sourceQueen),
            TargetQueenSummary    = DescribeQueen(targetQueen),
            KarencaUntil = inKarenca is null
                ? null
                : TreatmentStatusHelper.KarencaUntil(inKarenca.StartDate, inKarenca.EndDate, inKarenca.WithdrawalDays),
            KarencaProductName = inKarenca?.ProductName,
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads both hives and applies every check from SPEC-19 §3.1. Cross-apiary merges are allowed
    /// (D4), so the caller must be able to manage <b>both</b> apiaries — and both must belong to the
    /// same organization, which no single apiary check would catch.
    /// </summary>
    private async Task<(Beehive Source, Beehive Target)> LoadAndAuthorizePairAsync(
        int sourceId, int targetId, bool skipMergedChecks = false)
    {
        if (sourceId == targetId)
            throw new BusinessRuleException("Košnica se ne može sastaviti sama sa sobom.");

        var source = await _uow.Beehives.GetByIdAsync(sourceId)
            ?? throw new NotFoundException(nameof(Beehive), sourceId);
        var target = await _uow.Beehives.GetByIdAsync(targetId)
            ?? throw new NotFoundException(nameof(Beehive), targetId);

        await _access.EnsureCanManageApiaryAsync(source.ApiaryId);
        if (target.ApiaryId != source.ApiaryId)
            await _access.EnsureCanManageApiaryAsync(target.ApiaryId);

        var sourceApiary = await _uow.Apiaries.GetByIdAsync(source.ApiaryId);
        var targetApiary = await _uow.Apiaries.GetByIdAsync(target.ApiaryId);
        if (sourceApiary is null || targetApiary is null)
            throw new NotFoundException(nameof(Apiary), source.ApiaryId);

        if (sourceApiary.OrganizationId != targetApiary.OrganizationId)
            throw new BusinessRuleException("Košnice iz različitih organizacija se ne mogu sastaviti.");

        if (!skipMergedChecks)
        {
            if (source.MergedIntoBeehiveId is not null)
                throw new BusinessRuleException($"Košnica '{source.Name}' je već sastavljena s drugom košnicom.");

            if (target.MergedIntoBeehiveId is not null)
                throw new BusinessRuleException(
                    $"Košnica '{target.Name}' je i sama sastavljena s drugom košnicom, pa ne može primiti društvo.");
        }

        return (source, target);
    }

    private async Task<BeehiveMergeDto> LoadDtoAsync(int mergeId)
    {
        var merge = await _uow.BeehiveMerges.GetWithHivesAsync(mergeId)
            ?? throw new NotFoundException(nameof(BeehiveMerge), mergeId);
        return ToDto(merge, DateTime.UtcNow);
    }

    private static BeehiveMergeDto ToDto(BeehiveMerge m, DateTime utcNow) => new()
    {
        Id                = m.Id,
        SourceBeehiveId   = m.SourceBeehiveId,
        SourceBeehiveName = m.SourceBeehive?.Name ?? string.Empty,
        SourceApiaryId    = m.SourceBeehive?.ApiaryId ?? 0,
        TargetBeehiveId   = m.TargetBeehiveId,
        TargetBeehiveName = m.TargetBeehive?.Name ?? string.Empty,
        TargetApiaryId    = m.TargetBeehive?.ApiaryId ?? 0,
        MergedAt          = m.MergedAt,
        Reason            = m.Reason,
        ReasonName        = BsLabels.Label(m.Reason),
        Method            = m.Method,
        MethodName        = BsLabels.Label(m.Method),
        QueenOutcome      = m.QueenOutcome,
        QueenOutcomeName  = BsLabels.Label(m.QueenOutcome),
        Notes             = m.Notes,
        CreatedByName     = m.CreatedBy is null ? null : $"{m.CreatedBy.FirstName} {m.CreatedBy.LastName}",
        CreatedAt         = m.CreatedAt,
        CanUndoUntil      = MergeUndoPolicy.DeadlineFor(m, utcNow),
        UndoneAt          = m.UndoneAt,
    };

    private static string? DescribeQueen(Queen? queen) =>
        queen is null ? null : $"{queen.Year}, {BsLabels.Label(queen.MarkColor).ToLowerInvariant()}";

    private static string AppendLine(string? existing, string line) =>
        string.IsNullOrWhiteSpace(existing) ? line : $"{existing.TrimEnd()}\n{line}";

    /// <summary>
    /// SPEC-19 D6 — same audience as a new hive: whoever is responsible for the apiary but did not do
    /// this themselves. Failures are logged, never thrown: the merge is already committed.
    /// </summary>
    private async Task SendMergedNotificationsAsync(Beehive source, Beehive target)
    {
        if (_currentUser.UserId is not int actorId) return;

        try
        {
            var actor = await _uow.Users.GetByIdWithOrganizationAsync(actorId);
            var apiary = await _uow.Apiaries.GetByIdAsync(source.ApiaryId);
            if (actor is null || apiary is null) return;

            var title = "Sastavljena društva";
            var message =
                $"{actor.FirstName} {actor.LastName} je sastavio/la društvo iz košnice '{source.Name}' " +
                $"s košnicom '{target.Name}' (pčelinjak '{apiary.Name}'). Košnica '{source.Name}' više nije u pčelinjaku.";

            var recipients = actor.Role switch
            {
                UserRole.ApiaryAdmin => await _uow.Users.FindAsync(u =>
                    u.OrganizationId == apiary.OrganizationId && u.Role == UserRole.OrganizationAdmin),
                UserRole.OrganizationAdmin => await _uow.Users.FindAsync(u =>
                    u.ApiaryId == source.ApiaryId && u.Role == UserRole.ApiaryAdmin),
                UserRole.SystemAdmin => await _uow.Users.FindAsync(u =>
                    u.OrganizationId == apiary.OrganizationId && u.Role == UserRole.OrganizationAdmin),
                _ => [],
            };

            foreach (var recipient in recipients.Where(r => r.Id != actorId))
            {
                await _notifications.NotifyAsync(
                    recipient.Id, title, message,
                    NotificationType.BeehiveMerged, target.Id, nameof(Beehive));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Merge {SourceId} → {TargetId} committed, but notifying failed", source.Id, target.Id);
        }
    }
}
