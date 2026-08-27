using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.BeehiveMerges;
using Melarium.Application.Features.BeehiveMerges.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks SPEC-19's merge rules. The ones that matter most are the ones that fail silently rather than
/// loudly: the queen ordering for <see cref="MergeQueenOutcome.KeptSource"/> (wrong order = two active
/// queens in one hive), feeding being removed <b>per hive</b> rather than by stopping the programme
/// (wrong = every other hive on it stops being fed), and the treatment entry surviving with only a
/// note appended (wrong = a destroyed legal record).
/// </summary>
public class BeehiveMergeServiceTests
{
    private const int SourceHiveId = 10;
    private const int TargetHiveId = 20;
    private const int ApiaryId = 1;
    private const int OtherApiaryId = 2;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly ICurrentUser _currentUser = new TestCurrentUser { UserId = 7 };
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly BeehiveMergeService _service;

    private readonly List<BeehiveMerge> _addedMerges = [];
    private readonly List<Todo> _deletedTodos = [];
    private readonly List<Todo> _addedTodos = [];

    public BeehiveMergeServiceTests()
    {
        _service = new BeehiveMergeService(
            _uow, _currentUser, _access, _notifications,
            Substitute.For<ILogger<BeehiveMergeService>>());

        _uow.Beehives.GetByIdAsync(SourceHiveId).Returns(Hive(SourceHiveId, "Košnica 5", ApiaryId));
        _uow.Beehives.GetByIdAsync(TargetHiveId).Returns(Hive(TargetHiveId, "Košnica 3", ApiaryId));

        _uow.Apiaries.GetByIdAsync(ApiaryId).Returns(new Apiary { Id = ApiaryId, Name = "Gornji", OrganizationId = 1 });
        _uow.Apiaries.GetByIdAsync(OtherApiaryId).Returns(new Apiary { Id = OtherApiaryId, Name = "Donji", OrganizationId = 1 });

        _uow.Queens.GetActiveByBeehiveIdAsync(Arg.Any<int>()).Returns((Queen?)null);
        _uow.Todos.GetByBeehiveIdAsync(Arg.Any<int>()).Returns([]);
        _uow.Diets.GetByBeehiveAsync(Arg.Any<int>()).Returns([]);
        _uow.Treatments.GetByBeehiveAsync(Arg.Any<int>()).Returns([]);

        _uow.BeehiveMerges.AddAsync(Arg.Do<BeehiveMerge>(m => { m.Id = 99; _addedMerges.Add(m); }))
            .Returns(ci => ci.Arg<BeehiveMerge>());
        _uow.BeehiveMerges.GetWithHivesAsync(99).Returns(ci => _addedMerges.FirstOrDefault());

        _uow.Todos.DeleteAsync(Arg.Do<Todo>(t => _deletedTodos.Add(t))).Returns(Task.CompletedTask);
        _uow.Todos.AddAsync(Arg.Do<Todo>(t => _addedTodos.Add(t))).Returns(ci => ci.Arg<Todo>());

        // The org-admin path in the notification helper; irrelevant to what these tests assert.
        _uow.Users.GetByIdWithOrganizationAsync(7).Returns((User?)null);
    }

    private static Beehive Hive(int id, string name, int apiaryId) => new()
    {
        Id = id, Name = name, ApiaryId = apiaryId,
    };

    private static CreateBeehiveMergeDto Dto(
        MergeQueenOutcome queen = MergeQueenOutcome.KeptTarget,
        int source = SourceHiveId,
        int target = TargetHiveId) => new()
    {
        SourceBeehiveId = source,
        TargetBeehiveId = target,
        MergedAt        = DateTime.UtcNow.Date,
        Reason          = MergeReason.WeakColony,
        Method          = MergeMethod.Newspaper,
        QueenOutcome    = queen,
    };

    private static Queen ActiveQueen(int id, int hiveId) => new()
    {
        Id = id, BeehiveId = hiveId, Year = 2024,
        MarkColor = QueenMarkColor.Green, Status = QueenStatus.Active,
        IntroducedDate = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    // ── The hive leaves the apiary ─────────────────────────────────────────────

    [Fact]
    public async Task Merge_marks_the_source_hive_as_merged_into_the_target()
    {
        var source = await _uow.Beehives.GetByIdAsync(SourceHiveId);

        await _service.MergeAsync(Dto());

        Assert.Equal(TargetHiveId, source!.MergedIntoBeehiveId);
        Assert.NotNull(source.MergedAt);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Merge_writes_everything_in_one_SaveChanges()
    {
        _uow.Queens.GetActiveByBeehiveIdAsync(SourceHiveId).Returns(ActiveQueen(41, SourceHiveId));
        _uow.Todos.GetByBeehiveIdAsync(SourceHiveId).Returns([
            new Todo { Id = 1, Title = "Dodaj nastavak", BeehiveId = SourceHiveId, IsCompleted = false },
        ]);

        await _service.MergeAsync(Dto());

        // A partially-applied merge is the one state the undo journal cannot repair (SPEC-19 §3).
        await _uow.Received(1).SaveChangesAsync();
    }

    // ── Queens (D2) ────────────────────────────────────────────────────────────

    [Fact]
    public async Task KeptTarget_removes_only_the_source_queen()
    {
        var sourceQueen = ActiveQueen(41, SourceHiveId);
        var targetQueen = ActiveQueen(42, TargetHiveId);
        _uow.Queens.GetActiveByBeehiveIdAsync(SourceHiveId).Returns(sourceQueen);
        _uow.Queens.GetActiveByBeehiveIdAsync(TargetHiveId).Returns(targetQueen);

        await _service.MergeAsync(Dto(MergeQueenOutcome.KeptTarget));

        Assert.Equal(QueenStatus.Removed, sourceQueen.Status);
        Assert.NotNull(sourceQueen.EndDate);
        Assert.Equal(SourceHiveId, sourceQueen.BeehiveId);

        Assert.Equal(QueenStatus.Active, targetQueen.Status);
        Assert.Null(targetQueen.EndDate);
    }

    [Fact]
    public async Task KeptSource_closes_the_target_queen_then_moves_the_source_queen_over()
    {
        var sourceQueen = ActiveQueen(41, SourceHiveId);
        var targetQueen = ActiveQueen(42, TargetHiveId);
        _uow.Queens.GetActiveByBeehiveIdAsync(SourceHiveId).Returns(sourceQueen);
        _uow.Queens.GetActiveByBeehiveIdAsync(TargetHiveId).Returns(targetQueen);

        await _service.MergeAsync(Dto(MergeQueenOutcome.KeptSource));

        // The receiving hive must never hold two Active queens, not even transiently — that is the
        // state QueenService.UpdateAsync refuses outright (SPEC-19 §3.2).
        Assert.Equal(QueenStatus.Removed, targetQueen.Status);
        Assert.Equal(QueenStatus.Active, sourceQueen.Status);
        Assert.Equal(TargetHiveId, sourceQueen.BeehiveId);
        Assert.Single(new[] { sourceQueen, targetQueen }, q => q.Status == QueenStatus.Active);
    }

    [Fact]
    public async Task None_removes_both_queens()
    {
        var sourceQueen = ActiveQueen(41, SourceHiveId);
        var targetQueen = ActiveQueen(42, TargetHiveId);
        _uow.Queens.GetActiveByBeehiveIdAsync(SourceHiveId).Returns(sourceQueen);
        _uow.Queens.GetActiveByBeehiveIdAsync(TargetHiveId).Returns(targetQueen);

        await _service.MergeAsync(Dto(MergeQueenOutcome.None));

        Assert.Equal(QueenStatus.Removed, sourceQueen.Status);
        Assert.Equal(QueenStatus.Removed, targetQueen.Status);
    }

    [Fact]
    public async Task Queenless_hive_merges_without_error()
    {
        // Bezmatak is the single most common reason to merge (SPEC-19 §0) — it must not throw.
        await _service.MergeAsync(Dto(MergeQueenOutcome.KeptTarget));

        Assert.Single(_addedMerges);
    }

    [Fact]
    public async Task KeptSource_without_a_source_queen_is_refused()
    {
        _uow.Queens.GetActiveByBeehiveIdAsync(SourceHiveId).Returns((Queen?)null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.MergeAsync(Dto(MergeQueenOutcome.KeptSource)));
    }

    // ── Todos (D3) ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Merge_deletes_only_open_hive_todos()
    {
        var open = new Todo { Id = 1, Title = "Dodaj nastavak", BeehiveId = SourceHiveId, IsCompleted = false };
        var done = new Todo { Id = 2, Title = "Stari zadatak", BeehiveId = SourceHiveId, IsCompleted = true };
        _uow.Todos.GetByBeehiveIdAsync(SourceHiveId).Returns([open, done]);

        await _service.MergeAsync(Dto());

        Assert.Single(_deletedTodos);
        Assert.Equal(open.Id, _deletedTodos[0].Id);
    }

    // ── Feeding (D3) — the trap SPEC-19 §3.2 step 4 exists to prevent ──────────

    private Diet DietCovering(int id, string name, params int[] hiveIds)
    {
        var diet = new Diet { Id = id, Name = name, ApiaryId = ApiaryId, Status = DietStatus.InProgress };
        foreach (var hiveId in hiveIds)
            diet.Beehives.Add(new DietBeehive { Id = 100 + hiveId, DietId = id, BeehiveId = hiveId });
        _uow.Diets.GetWithEntriesAsync(id).Returns(diet);
        return diet;
    }

    [Fact]
    public async Task Merge_takes_the_hive_off_the_programme_and_leaves_the_programme_running()
    {
        var diet = DietCovering(5, "Zimska pogača", SourceHiveId, TargetHiveId, 30);
        _uow.Diets.GetByBeehiveAsync(SourceHiveId).Returns([diet]);

        await _service.MergeAsync(Dto());

        // Since SPEC-12 a diet is an apiary-level programme. Stopping it would end the feeding for
        // every other hive on it — the whole point of removing the hive instead.
        Assert.Equal(DietStatus.InProgress, diet.Status);
        Assert.Null(diet.EarlyCompletionComment);
        Assert.NotNull(diet.Beehives.First(db => db.BeehiveId == SourceHiveId).RemovedOn);
        Assert.Null(diet.Beehives.First(db => db.BeehiveId == TargetHiveId).RemovedOn);
    }

    [Fact]
    public async Task Merge_stops_the_programme_early_only_when_it_was_the_last_hive()
    {
        var diet = DietCovering(5, "Zimska pogača", SourceHiveId);
        _uow.Diets.GetByBeehiveAsync(SourceHiveId).Returns([diet]);

        await _service.MergeAsync(Dto());

        Assert.Equal(DietStatus.StoppedEarly, diet.Status);
        Assert.Contains("sastavljeno", diet.EarlyCompletionComment!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Undo_clears_RemovedOn_on_the_same_row()
    {
        var diet = DietCovering(5, "Zimska pogača", SourceHiveId, TargetHiveId);
        _uow.Diets.GetByBeehiveAsync(SourceHiveId).Returns([diet]);

        await _service.MergeAsync(Dto());
        var link = diet.Beehives.First(db => db.BeehiveId == SourceHiveId);
        var originalRowId = link.Id;
        var originalCreatedAt = link.CreatedAt;

        await _service.UndoAsync(_addedMerges.Single().Id);

        // A fresh membership row would carry a new CreatedAt — the "when the hive joined" the
        // consumption maths reads (see DietBeehive's own note).
        var restored = diet.Beehives.Single(db => db.BeehiveId == SourceHiveId);
        Assert.Equal(originalRowId, restored.Id);
        Assert.Equal(originalCreatedAt, restored.CreatedAt);
        Assert.Null(restored.RemovedOn);
    }

    // ── Treatments (D3) — the legal record must survive ────────────────────────

    private Treatment OngoingTreatment(int id, string product, int hiveId, string? doseNote = null)
    {
        var treatment = new Treatment
        {
            Id = id, ApiaryId = ApiaryId, ProductName = product,
            StartDate = DateTime.UtcNow.Date.AddDays(-3), EndDate = null,
        };
        treatment.Entries.Add(new TreatmentEntry { Id = 200 + hiveId, TreatmentId = id, BeehiveId = hiveId, DoseNote = doseNote });
        _uow.Treatments.GetWithEntriesAsync(id).Returns(treatment);
        return treatment;
    }

    [Fact]
    public async Task Merge_annotates_the_treatment_entry_and_never_deletes_it()
    {
        var treatment = OngoingTreatment(7, "Apivar", SourceHiveId);
        _uow.Treatments.GetByBeehiveAsync(SourceHiveId).Returns([treatment]);

        await _service.MergeAsync(Dto());

        var entry = Assert.Single(treatment.Entries);
        Assert.Equal(SourceHiveId, entry.BeehiveId);
        Assert.Contains("Prekinuto", entry.DoseNote!);
        Assert.Contains("Košnica 3", entry.DoseNote!);
    }

    [Fact]
    public async Task Merge_leaves_a_finished_treatment_alone()
    {
        var finished = OngoingTreatment(7, "Apivar", SourceHiveId);
        finished.EndDate = DateTime.UtcNow.Date.AddDays(-1);
        _uow.Treatments.GetByBeehiveAsync(SourceHiveId).Returns([finished]);

        await _service.MergeAsync(Dto());

        Assert.Null(finished.Entries[0].DoseNote);
    }

    [Fact]
    public async Task Undo_restores_the_previous_dose_note()
    {
        var treatment = OngoingTreatment(7, "Apivar", SourceHiveId, doseNote: "pola trake");
        _uow.Treatments.GetByBeehiveAsync(SourceHiveId).Returns([treatment]);

        await _service.MergeAsync(Dto());
        await _service.UndoAsync(_addedMerges.Single().Id);

        Assert.Equal("pola trake", treatment.Entries[0].DoseNote);
    }

    // ── Guards (§3.1) ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_hive_cannot_be_merged_with_itself()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.MergeAsync(Dto(source: SourceHiveId, target: SourceHiveId)));
    }

    [Fact]
    public async Task An_already_merged_hive_cannot_be_merged_again()
    {
        var source = await _uow.Beehives.GetByIdAsync(SourceHiveId);
        source!.MergedIntoBeehiveId = 999;

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.MergeAsync(Dto()));
    }

    [Fact]
    public async Task An_already_merged_hive_cannot_receive_a_colony()
    {
        var target = await _uow.Beehives.GetByIdAsync(TargetHiveId);
        target!.MergedIntoBeehiveId = 999;

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.MergeAsync(Dto()));
    }

    [Fact]
    public async Task Hives_from_two_organizations_cannot_be_merged()
    {
        _uow.Beehives.GetByIdAsync(TargetHiveId).Returns(Hive(TargetHiveId, "Košnica 3", OtherApiaryId));
        _uow.Apiaries.GetByIdAsync(OtherApiaryId)
            .Returns(new Apiary { Id = OtherApiaryId, Name = "Tuđi", OrganizationId = 2 });

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.MergeAsync(Dto()));
    }

    [Fact]
    public async Task A_cross_apiary_merge_checks_both_apiaries()
    {
        _uow.Beehives.GetByIdAsync(TargetHiveId).Returns(Hive(TargetHiveId, "Košnica 3", OtherApiaryId));

        await _service.MergeAsync(Dto());

        // D4 allows crossing apiaries — but only for someone who manages both.
        await _access.Received(1).EnsureCanManageApiaryAsync(ApiaryId);
        await _access.Received(1).EnsureCanManageApiaryAsync(OtherApiaryId);
    }

    [Fact]
    public async Task A_future_merge_date_is_refused()
    {
        var dto = Dto();
        dto.MergedAt = DateTime.UtcNow.Date.AddDays(5);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.MergeAsync(dto));
    }

    // ── Undo (§4) ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Undo_restores_the_hive_the_queen_and_the_deleted_todos()
    {
        var sourceQueen = ActiveQueen(41, SourceHiveId);
        _uow.Queens.GetActiveByBeehiveIdAsync(SourceHiveId).Returns(sourceQueen);
        _uow.Queens.GetByIdAsync(41).Returns(sourceQueen);
        _uow.Todos.GetByBeehiveIdAsync(SourceHiveId).Returns([
            new Todo
            {
                Id = 1, Title = "Dodaj nastavak", BeehiveId = SourceHiveId, IsCompleted = false,
                Priority = TodoPriority.High, DueDate = new DateTime(2026, 9, 1), AssignedToId = 3,
            },
        ]);

        await _service.MergeAsync(Dto());
        var merge = _addedMerges.Single();

        await _service.UndoAsync(merge.Id);

        var source = await _uow.Beehives.GetByIdAsync(SourceHiveId);
        Assert.Null(source!.MergedIntoBeehiveId);
        Assert.Null(source.MergedAt);
        Assert.NotNull(merge.UndoneAt);

        Assert.Equal(QueenStatus.Active, sourceQueen.Status);
        Assert.Null(sourceQueen.EndDate);

        var restored = Assert.Single(_addedTodos);
        Assert.Equal("Dodaj nastavak", restored.Title);
        Assert.Equal(TodoPriority.High, restored.Priority);
        Assert.Equal(new DateTime(2026, 9, 1), restored.DueDate);
        Assert.Equal(3, restored.AssignedToId);
        Assert.Equal(SourceHiveId, restored.BeehiveId);
    }

    [Fact]
    public async Task Undo_after_the_window_closed_is_refused()
    {
        await _service.MergeAsync(Dto());
        var merge = _addedMerges.Single();
        merge.CreatedAt = DateTime.UtcNow.AddHours(-25);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UndoAsync(merge.Id));
    }

    [Fact]
    public async Task Undo_twice_is_refused()
    {
        await _service.MergeAsync(Dto());
        var merge = _addedMerges.Single();

        await _service.UndoAsync(merge.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UndoAsync(merge.Id));
    }
}
