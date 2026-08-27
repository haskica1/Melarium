using Melarium.Application.Common.Exceptions;
using Melarium.Application.Features.Inspections;
using Melarium.Application.Features.Inspections.DTOs;
using Melarium.Application.Features.Inspections.Validators;
using Melarium.Application.Features.Todos;
using Melarium.Application.Features.Todos.DTOs;
using Melarium.Application.Features.Todos.Validators;
using Melarium.Application.Features.Assistant;
using Melarium.Application.Features.BeehiveMerges;
using Melarium.Application.Features.BeehiveMerges.DTOs;
using Melarium.Application.Features.BeehiveMerges.Validators;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Direct tests of the real executor (SPEC-17 §5.1) — Phase A shipped without these because every
/// other assistant test mocks <see cref="IAiActionExecutor"/> instead. Phase C's field-preservation
/// logic ("null in the AI's fields means don't touch this") is exactly the kind of thing that silently
/// clears a field if it regresses, so it gets tested against the executor itself, not just the
/// interface. Real FluentValidation validators are used throughout — they have no dependencies, and a
/// mocked "always valid" stub would defeat SPEC-17 §5.2's whole point.
/// </summary>
public class AiActionExecutorTests
{
    private readonly ITodoService _todos = Substitute.For<ITodoService>();
    private readonly IInspectionService _inspections = Substitute.For<IInspectionService>();
    private readonly IBeehiveMergeService _merges = Substitute.For<IBeehiveMergeService>();

    private AiActionExecutor Executor() => new(
        _inspections, _todos, _merges,
        new CreateInspectionValidator(), new CreateTodoValidator(),
        new UpdateTodoValidator(), new UpdateInspectionValidator(),
        new CreateBeehiveMergeValidator(),
        Substitute.For<IConfiguration>(), Substitute.For<ILogger<AiActionExecutor>>());

    // ── MergeBeehive (SPEC-19 §8) ──────────────────────────────────────────

    private static AiActionPayload MergePayload(AiActionFields fields) =>
        new(1, "Zlatna dolina", 11, "Košnica 5", AiResolutionIssue.None, [], fields);

    [Fact]
    public async Task MergeBeehive_calls_the_merge_service_with_both_hives_and_the_chosen_queen()
    {
        CreateBeehiveMergeDto? captured = null;
        _merges.MergeAsync(Arg.Do<CreateBeehiveMergeDto>(d => captured = d))
            .Returns(new BeehiveMergeDto { Id = 77 });

        var result = await Executor().ExecuteAsync(AiActionKind.MergeBeehive, MergePayload(
            new AiActionFields(
                TargetBeehiveId: 12,
                TargetBeehiveName: "Košnica 3",
                QueenOutcome: MergeQueenOutcome.KeptSource,
                MergeReason: MergeReason.Queenless)));

        Assert.True(result.Succeeded);
        Assert.Equal(77, result.EntityId);
        Assert.Equal(11, captured!.SourceBeehiveId);
        Assert.Equal(12, captured.TargetBeehiveId);
        Assert.Equal(MergeQueenOutcome.KeptSource, captured.QueenOutcome);
        Assert.Equal(MergeReason.Queenless, captured.Reason);
    }

    [Fact]
    public async Task MergeBeehive_without_a_queen_outcome_fails_instead_of_defaulting()
    {
        var result = await Executor().ExecuteAsync(AiActionKind.MergeBeehive, MergePayload(
            new AiActionFields(TargetBeehiveId: 12, TargetBeehiveName: "Košnica 3")));

        // Defaulting here would silently pick which queen dies (SPEC-19 §8).
        Assert.False(result.Succeeded);
        await _merges.DidNotReceive().MergeAsync(Arg.Any<CreateBeehiveMergeDto>());
    }

    [Fact]
    public async Task MergeBeehive_without_a_receiving_hive_fails()
    {
        var result = await Executor().ExecuteAsync(AiActionKind.MergeBeehive, MergePayload(
            new AiActionFields(QueenOutcome: MergeQueenOutcome.KeptTarget)));

        Assert.False(result.Succeeded);
        await _merges.DidNotReceive().MergeAsync(Arg.Any<CreateBeehiveMergeDto>());
    }

    [Fact]
    public async Task MergeBeehive_runs_the_validator_the_controller_runs()
    {
        // Same hive twice — the validator's rule, not the service's (SPEC-17 §5.2).
        var result = await Executor().ExecuteAsync(AiActionKind.MergeBeehive, MergePayload(
            new AiActionFields(
                TargetBeehiveId: 11,
                TargetBeehiveName: "Košnica 5",
                QueenOutcome: MergeQueenOutcome.KeptTarget)));

        Assert.False(result.Succeeded);
        await _merges.DidNotReceive().MergeAsync(Arg.Any<CreateBeehiveMergeDto>());
    }

    [Fact]
    public async Task MergeBeehive_surfaces_the_services_own_message()
    {
        _merges.MergeAsync(Arg.Any<CreateBeehiveMergeDto>())
            .Throws(new BusinessRuleException("Košnica 'Košnica 5' je već sastavljena s drugom košnicom."));

        var result = await Executor().ExecuteAsync(AiActionKind.MergeBeehive, MergePayload(
            new AiActionFields(
                TargetBeehiveId: 12,
                TargetBeehiveName: "Košnica 3",
                QueenOutcome: MergeQueenOutcome.KeptTarget)));

        Assert.False(result.Succeeded);
        Assert.Contains("već sastavljena", result.ErrorMessage!);
    }

    private static AiActionPayload ExistingTodoPayload(int? existingId, AiActionFields fields) =>
        new(1, "Zlatna dolina", 11, "Košnica 2", AiResolutionIssue.None, [], fields,
            existingId, "Todo", "Dodaj postolje");

    private static AiActionPayload ExistingInspectionPayload(int? existingId, AiActionFields fields) =>
        new(1, "Zlatna dolina", 11, "Košnica 2", AiResolutionIssue.None, [], fields,
            existingId, "Inspection", "Pregled 05.08.2026");

    private static readonly TodoDto ExistingTodo = new()
    {
        Id = 501, Title = "Dodaj postolje", Notes = "stara napomena",
        DueDate = new DateTime(2026, 8, 20), Priority = TodoPriority.Low,
        IsCompleted = false, AssignedToId = 9,
    };

    private static readonly InspectionDto ExistingInspection = new()
    {
        Id = 601, Date = new DateTime(2026, 8, 5), HoneyLevel = HoneyLevel.Low,
        BroodStatus = "staro stanje", Notes = "stara napomena", BeehiveId = 11,
    };

    private static AiActionPayload CreatePayload(AiActionFields fields) =>
        new(1, "Zlatna dolina", 11, "Košnica 2", AiResolutionIssue.None, [], fields);

    /// <summary>
    /// Regression for ADR-037: <c>DateOnly.ToDateTime</c> yields <c>Kind=Unspecified</c>, which
    /// Npgsql refuses to write to a timestamptz column. These four call sites are the only
    /// producers of such a value in the application, and they took assistant confirmation down with
    /// a 500 after the record had already been written. The merge date joined them with SPEC-19 —
    /// it lands on <c>BeehiveMerge.MergedAt</c> and <c>Beehive.MergedAt</c>, both persisted.
    /// </summary>
    [Fact]
    public async Task Every_date_the_assistant_writes_is_marked_Utc()
    {
        CreateInspectionDto? created = null;
        _inspections.CreateAsync(Arg.Do<CreateInspectionDto>(d => created = d))
            .Returns(new InspectionDto { Id = 1 });

        CreateTodoDto? newTodo = null;
        _todos.CreateAsync(Arg.Do<CreateTodoDto>(d => newTodo = d)).Returns(ExistingTodo);

        _todos.GetByIdAsync(501).Returns(ExistingTodo);
        UpdateTodoDto? updatedTodo = null;
        _todos.UpdateAsync(501, Arg.Do<UpdateTodoDto>(d => updatedTodo = d)).Returns(ExistingTodo);

        CreateBeehiveMergeDto? merge = null;
        _merges.MergeAsync(Arg.Do<CreateBeehiveMergeDto>(d => merge = d))
            .Returns(new BeehiveMergeDto { Id = 77 });

        var executor = Executor();
        await executor.ExecuteAsync(AiActionKind.CreateInspection,
            CreatePayload(new AiActionFields(Date: new DateOnly(2026, 8, 5))));
        await executor.ExecuteAsync(AiActionKind.CreateTodo,
            CreatePayload(new AiActionFields(Title: "Dodaj postolje", DueDate: new DateOnly(2026, 8, 25))));
        await executor.ExecuteAsync(AiActionKind.UpdateTodo,
            ExistingTodoPayload(501, new AiActionFields(DueDate: new DateOnly(2026, 8, 25))));
        await executor.ExecuteAsync(AiActionKind.MergeBeehive, MergePayload(
            new AiActionFields(
                Date: new DateOnly(2026, 8, 5),
                TargetBeehiveId: 12,
                TargetBeehiveName: "Košnica 3",
                QueenOutcome: MergeQueenOutcome.KeptSource)));

        Assert.Equal(DateTimeKind.Utc, created!.Date.Kind);
        Assert.Equal(DateTimeKind.Utc, newTodo!.DueDate!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, updatedTodo!.DueDate!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, merge!.MergedAt.Kind);
    }

    // ── UpdateTodo: only what the AI mentioned changes ──────────────────────

    [Fact]
    public async Task UpdateTodo_changes_only_the_mentioned_field_and_preserves_the_rest()
    {
        _todos.GetByIdAsync(501).Returns(ExistingTodo);
        UpdateTodoDto? captured = null;
        _todos.UpdateAsync(501, Arg.Do<UpdateTodoDto>(d => captured = d))
            .Returns(ExistingTodo);

        // The AI mentioned only a new due date — nothing else.
        var payload = ExistingTodoPayload(501, new AiActionFields(DueDate: new DateOnly(2026, 8, 25)));

        var outcome = await Executor().ExecuteAsync(AiActionKind.UpdateTodo, payload);

        Assert.True(outcome.Succeeded);
        Assert.Equal(new DateTime(2026, 8, 25), captured!.DueDate);
        Assert.Equal("Dodaj postolje", captured.Title);          // untouched
        Assert.Equal("stara napomena", captured.Notes);          // untouched
        Assert.Equal(TodoPriority.Low, captured.Priority);       // untouched
        Assert.Equal(9, captured.AssignedToId);                  // untouched — a real bug here loses the assignee
        Assert.False(captured.IsCompleted);                      // UpdateTodo never flips completion
    }

    [Fact]
    public async Task UpdateTodo_with_no_existing_id_fails_without_touching_the_service()
    {
        var outcome = await Executor().ExecuteAsync(AiActionKind.UpdateTodo, ExistingTodoPayload(null, new AiActionFields()));

        Assert.False(outcome.Succeeded);
        await _todos.DidNotReceive().GetByIdAsync(Arg.Any<int>());
        await _todos.DidNotReceive().UpdateAsync(Arg.Any<int>(), Arg.Any<UpdateTodoDto>());
    }

    [Fact]
    public async Task A_whitespace_only_title_is_treated_as_no_change_not_as_a_blank_one()
    {
        _todos.GetByIdAsync(501).Returns(ExistingTodo);
        _todos.UpdateAsync(501, Arg.Any<UpdateTodoDto>()).Returns(ExistingTodo);

        // "   " means the AI did not actually rename the todo — Trimmed() maps it to null, which
        // falls back to the current title, so the real UpdateTodoValidator never sees a blank one.
        var payload = ExistingTodoPayload(501, new AiActionFields(Title: "   "));

        var outcome = await Executor().ExecuteAsync(AiActionKind.UpdateTodo, payload);

        Assert.True(outcome.Succeeded);
        await _todos.Received(1).UpdateAsync(501, Arg.Is<UpdateTodoDto>(d => d.Title == "Dodaj postolje"));
    }

    // ── CompleteTodo ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteTodo_flips_IsCompleted_and_preserves_everything_else()
    {
        _todos.GetByIdAsync(501).Returns(ExistingTodo);
        UpdateTodoDto? captured = null;
        _todos.UpdateAsync(501, Arg.Do<UpdateTodoDto>(d => captured = d)).Returns(ExistingTodo);

        var outcome = await Executor().ExecuteAsync(AiActionKind.CompleteTodo, ExistingTodoPayload(501, new AiActionFields()));

        Assert.True(outcome.Succeeded);
        Assert.True(captured!.IsCompleted);
        Assert.Equal("Dodaj postolje", captured.Title);
        Assert.Equal(TodoPriority.Low, captured.Priority);
    }

    [Fact]
    public async Task CompleteTodo_on_an_already_completed_todo_fails_without_calling_UpdateAsync()
    {
        var alreadyDone = new TodoDto
        {
            Id = 501, Title = "Dodaj postolje", Notes = "stara napomena",
            DueDate = new DateTime(2026, 8, 20), Priority = TodoPriority.Low,
            IsCompleted = true, AssignedToId = 9,
        };
        _todos.GetByIdAsync(501).Returns(alreadyDone);

        var outcome = await Executor().ExecuteAsync(AiActionKind.CompleteTodo, ExistingTodoPayload(501, new AiActionFields()));

        Assert.False(outcome.Succeeded);
        await _todos.DidNotReceive().UpdateAsync(Arg.Any<int>(), Arg.Any<UpdateTodoDto>());
    }

    // ── DeleteTodo ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTodo_calls_the_service_with_the_resolved_id()
    {
        var outcome = await Executor().ExecuteAsync(AiActionKind.DeleteTodo, ExistingTodoPayload(501, new AiActionFields()));

        Assert.True(outcome.Succeeded);
        Assert.Equal(501, outcome.EntityId);
        await _todos.Received(1).DeleteAsync(501);
    }

    [Fact]
    public async Task DeleteTodo_with_no_existing_id_never_calls_DeleteAsync()
    {
        var outcome = await Executor().ExecuteAsync(AiActionKind.DeleteTodo, ExistingTodoPayload(null, new AiActionFields()));

        Assert.False(outcome.Succeeded);
        await _todos.DidNotReceive().DeleteAsync(Arg.Any<int>());
    }

    // ── UpdateInspection ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateInspection_changes_only_the_mentioned_field_and_never_the_date_or_hive()
    {
        _inspections.GetByIdAsync(601).Returns(ExistingInspection);
        UpdateInspectionDto? captured = null;
        _inspections.UpdateAsync(601, Arg.Do<UpdateInspectionDto>(d => captured = d))
            .Returns(ExistingInspection);

        // The AI mentioned only the honey level — date identified which inspection, it is not a new value.
        var payload = ExistingInspectionPayload(601, new AiActionFields(Date: new DateOnly(2026, 8, 5), HoneyLevel: HoneyLevel.High));

        var outcome = await Executor().ExecuteAsync(AiActionKind.UpdateInspection, payload);

        Assert.True(outcome.Succeeded);
        Assert.Equal(HoneyLevel.High, captured!.HoneyLevel);
        Assert.Equal("staro stanje", captured.BroodStatus);       // untouched
        Assert.Equal("stara napomena", captured.Notes);           // untouched
        Assert.Equal(ExistingInspection.Date, captured.Date);     // never moved
        Assert.Equal(11, captured.BeehiveId);                     // never moved
    }

    [Fact]
    public async Task UpdateInspection_with_no_existing_id_fails_without_touching_the_service()
    {
        var outcome = await Executor().ExecuteAsync(AiActionKind.UpdateInspection, ExistingInspectionPayload(null, new AiActionFields()));

        Assert.False(outcome.Succeeded);
        await _inspections.DidNotReceive().GetByIdAsync(Arg.Any<int>());
    }

    // ── DeleteInspection ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteInspection_calls_the_service_with_the_resolved_id()
    {
        var outcome = await Executor().ExecuteAsync(AiActionKind.DeleteInspection, ExistingInspectionPayload(601, new AiActionFields()));

        Assert.True(outcome.Succeeded);
        Assert.Equal(601, outcome.EntityId);
        await _inspections.Received(1).DeleteAsync(601);
    }

    [Fact]
    public async Task DeleteInspection_with_no_existing_id_never_calls_DeleteAsync()
    {
        var outcome = await Executor().ExecuteAsync(AiActionKind.DeleteInspection, ExistingInspectionPayload(null, new AiActionFields()));

        Assert.False(outcome.Succeeded);
        await _inspections.DidNotReceive().DeleteAsync(Arg.Any<int>());
    }

    // ── A record that changed underneath the proposal (SPEC-17 §5.3) ─────────

    [Fact]
    public async Task A_record_deleted_between_propose_and_confirm_fails_gracefully_not_with_an_exception()
    {
        _todos.GetByIdAsync(501).ThrowsAsync(new NotFoundException(nameof(Domain.Entities.Todo), 501));

        var outcome = await Executor().ExecuteAsync(AiActionKind.UpdateTodo, ExistingTodoPayload(501, new AiActionFields()));

        Assert.False(outcome.Succeeded);
        Assert.NotNull(outcome.ErrorMessage);
    }

    [Fact]
    public async Task Access_revoked_between_propose_and_confirm_fails_gracefully_not_with_an_exception()
    {
        _inspections.GetByIdAsync(601).Returns(ExistingInspection);
        _inspections.UpdateAsync(601, Arg.Any<UpdateInspectionDto>())
            .ThrowsAsync(new ForbiddenAccessException());

        var payload = ExistingInspectionPayload(601, new AiActionFields(HoneyLevel: HoneyLevel.High));
        var outcome = await Executor().ExecuteAsync(AiActionKind.UpdateInspection, payload);

        Assert.False(outcome.Succeeded);
        Assert.NotNull(outcome.ErrorMessage);
    }
}
