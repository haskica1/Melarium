using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Assistant;
using Melarium.Application.Features.Assistant.DTOs;
using Melarium.Application.Features.Inspections;
using Melarium.Application.Features.Inspections.DTOs;
using Melarium.Application.Features.Todos;
using Melarium.Application.Features.Todos.DTOs;
using Melarium.Application.Features.Weather;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks the assistant's safety rules (SPEC-17 §5): ownership, the plan quota, nothing persisted on an
/// AI failure, the pre-flight that hides impossible actions, the untrusted confirm payload, per-action
/// honesty on partial failure, and the double-confirm guard. Groq is mocked via
/// <see cref="IAssistantAiClient"/> — no network, no database.
/// </summary>
public class AiAssistantServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly IAssistantAiClient _ai = Substitute.For<IAssistantAiClient>();
    private readonly ITranscriptionService _transcription = Substitute.For<ITranscriptionService>();
    private readonly IAiActionExecutor _executor = Substitute.For<IAiActionExecutor>();
    private readonly IPlanGuard _plan = Substitute.For<IPlanGuard>();
    private readonly ITodoService _todos = Substitute.For<ITodoService>();
    private readonly IInspectionService _inspections = Substitute.For<IInspectionService>();
    private readonly IWeatherService _weather = Substitute.For<IWeatherService>();

    private static readonly Apiary Zlatna = new() { Id = 1, Name = "Zlatna dolina" };
    private static readonly Beehive Hive2 = new() { Id = 11, Name = "Košnica 2", LabelNumber = "2", ApiaryId = 1 };

    public AiAssistantServiceTests()
    {
        _access.GetAccessibleApiariesAsync().Returns<IReadOnlyList<Apiary>>([Zlatna]);
        _access.GetAccessibleBeehivesAsync().Returns<IReadOnlyList<Beehive>>([Hive2]);
        _access.CanManageApiaryAsync(Arg.Any<int>()).Returns(true);
        // Phase C's search pools are empty by default — only tests exercising update/complete/delete
        // set up specific rows, so every Phase A/B test keeps behaving exactly as before.
        _todos.GetAllOpenForCurrentUserAsync().Returns(Enumerable.Empty<TodoDto>());
        _inspections.GetByBeehiveIdAsync(Arg.Any<int>()).Returns(Enumerable.Empty<InspectionDto>());
    }

    /// <summary>Substituted rather than built — same approach as <see cref="PlanGuardTests"/>, no extra package.</summary>
    private static IConfiguration Config(params (string Key, string Value)[] values)
    {
        var lookup = values.ToDictionary(v => v.Key, v => v.Value);
        var config = Substitute.For<IConfiguration>();
        config[Arg.Any<string>()].Returns(ci => lookup.GetValueOrDefault(ci.Arg<string>()));
        return config;
    }

    private AiAssistantService Service(int userId = 1, int? orgId = 7, IConfiguration? config = null) =>
        new(_uow, _access,
            new TestCurrentUser { UserId = userId, Role = UserRole.OrganizationAdmin, OrganizationId = orgId },
            _ai, _transcription, _executor, _plan, _todos, _inspections, _weather, config ?? Config());

    /// <summary>Enough repository setup for <c>BuildHiveContextBlockAsync</c> to run to completion
    /// without a null reference — apiary stays unresolved (name "—", no coordinates), which also
    /// skips the weather branch entirely, so <see cref="_weather"/> needs no configuration here.</summary>
    private void WireEmptyHiveData(int beehiveId)
    {
        _uow.Beehives.GetByIdAsync(beehiveId).Returns(new Beehive { Id = beehiveId, Name = "Košnica 2", ApiaryId = 1 });
        _uow.Inspections.GetByBeehiveIdAsync(beehiveId).Returns(Enumerable.Empty<Inspection>());
        _uow.Diets.GetActiveForBeehivesAsync(Arg.Any<IReadOnlyCollection<int>>()).Returns(new Dictionary<int, List<DietActiveInfo>>());
        _uow.Todos.GetByBeehiveIdAsync(beehiveId).Returns(Enumerable.Empty<Todo>());
        _uow.Harvests.GetHiveYearlyTotalsAsync(beehiveId).Returns(new Dictionary<int, decimal>());
        _uow.Treatments.GetLatestForBeehivesAsync(Arg.Any<IReadOnlyCollection<int>>()).Returns(new Dictionary<int, TreatmentLatestInfo>());
    }

    private void AiReturns(string json) =>
        _ai.SendAsync(Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<CancellationToken>()).Returns(json);

    private const string OneInspection =
        """
        { "reply": "Unosim pregled.", "actions": [
          { "kind": "create_inspection", "apiary": "Zlatna dolina", "hives": ["2"],
            "fields": { "honeyLevel": 2, "broodStatus": "5 ramova legla" } } ] }
        """;

    // ── Ownership ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_users_session_is_a_404_never_a_403()
    {
        _uow.AiAssistantSessions.GetWithTurnsAsync(5)
            .Returns(new AiAssistantSession { Id = 5, UserId = 2 });

        await Assert.ThrowsAsync<NotFoundException>(() => Service(userId: 1).GetSessionAsync(5));
    }

    [Fact]
    public async Task Another_users_turn_cannot_be_confirmed()
    {
        var turn = new AiAssistantTurn
        {
            Id = 9,
            Session = new AiAssistantSession { Id = 5, UserId = 2 },
            Actions = { new AiAssistantAction { Id = 1, Status = AiActionStatus.Pending } },
        };
        _uow.AiAssistantSessions.GetTurnWithActionsAsync(9).Returns(turn);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Service(userId: 1).ConfirmAsync(9, new ConfirmActionsDto()));

        await _executor.DidNotReceive().ExecuteAsync(Arg.Any<AiActionKind>(), Arg.Any<AiActionPayload>());
    }

    [Fact]
    public async Task Deleting_another_users_session_is_a_404()
    {
        _uow.AiAssistantSessions.GetByIdAsync(5).Returns(new AiAssistantSession { Id = 5, UserId = 2 });

        await Assert.ThrowsAsync<NotFoundException>(() => Service(userId: 1).DeleteSessionAsync(5));
        await _uow.AiAssistantSessions.DidNotReceive().DeleteAsync(Arg.Any<AiAssistantSession>());
    }

    // ── Plan quota ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_plan_gate_runs_before_the_model_is_ever_called()
    {
        _plan.EnsureAiInteractionAsync(7).ThrowsAsync(new PlanLimitException("Free"));

        await Assert.ThrowsAsync<PlanLimitException>(
            () => Service().StartSessionAsync(new StartAssistantSessionDto { Text = "pregled" }));

        await _ai.DidNotReceive().SendAsync(Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_org_less_caller_bypasses_the_quota_rather_than_crashing()
    {
        AiReturns(OneInspection);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service(orgId: null).StartSessionAsync(new StartAssistantSessionDto { Text = "pregled" });

        await _plan.DidNotReceive().EnsureAiInteractionAsync(Arg.Any<int>());
    }

    // ── Transactional AI ──────────────────────────────────────────────────────

    [Fact]
    public async Task An_ai_failure_persists_nothing_so_the_users_words_are_not_lost()
    {
        _ai.SendAsync(Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("groq down"));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().StartSessionAsync(new StartAssistantSessionDto { Text = "pregled košnice 2" }));

        await _uow.AiAssistantSessions.DidNotReceive().AddAsync(Arg.Any<AiAssistantSession>());
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Unreadable_model_output_becomes_a_message_not_an_exception()
    {
        AiReturns("ovo nije JSON");
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "nešto" });

        var assistantTurn = saved!.Turns.Last();
        Assert.Empty(assistantTurn.Actions);
        Assert.Contains("Nisam uspio razumjeti", assistantTurn.Content);
        Assert.Equal("ovo nije JSON", assistantTurn.RawModelJson);
    }

    // ── Proposals ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_resolved_command_is_stored_as_a_pending_proposal_and_writes_nothing_yet()
    {
        AiReturns(OneInspection);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "Pregledana košnica 2" });

        var action = Assert.Single(saved!.Turns.Last().Actions);
        Assert.Equal(AiActionKind.CreateInspection, action.Kind);
        Assert.Equal(AiActionStatus.Pending, action.Status);
        Assert.Equal("Zlatna dolina · Košnica 2", action.TargetSummary);

        // Proposing must never execute.
        await _executor.DidNotReceive().ExecuteAsync(Arg.Any<AiActionKind>(), Arg.Any<AiActionPayload>());
    }

    [Fact]
    public async Task The_raw_transcript_is_kept_for_the_history()
    {
        AiReturns(OneInspection);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service().StartSessionAsync(new StartAssistantSessionDto
        {
            Text = "Pregledana košnica 2",
            Transcript = "pregledana kosnica dva ovaj",
        });

        Assert.Equal("pregledana kosnica dva ovaj", saved!.Turns.First().Transcript);
    }

    [Fact]
    public async Task Pre_flight_drops_an_apiary_todo_a_beekeeper_could_not_create()
    {
        // TodoService requires management rights for an apiary-level todo; offering a card that is
        // certain to 403 would be a worse experience than not offering it.
        _access.CanManageApiaryAsync(1).Returns(false);
        AiReturns("""
            { "reply": "ok", "actions": [
              { "kind": "create_todo", "apiary": "Zlatna dolina", "hives": null,
                "fields": { "title": "Dodaj postolje" } } ] }
            """);

        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "dodaj zadatak" });

        Assert.Empty(saved!.Turns.Last().Actions);
    }

    // ── Clarification (Phase B) ──────────────────────────────────────────────

    [Fact]
    public async Task An_unresolved_apiary_produces_tappable_candidates_on_the_response()
    {
        AiReturns("""
            { "reply": "Nisam prepoznao taj pčelinjak.", "actions": [
              { "kind": "create_inspection", "apiary": "Nepostojeći", "hives": ["2"], "fields": {} } ] }
            """);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        var result = await Service().StartSessionAsync(new StartAssistantSessionDto
        {
            Text = "Pregledana košnica 2 na pčelinjaku Nepostojeći",
        });

        var candidate = Assert.Single(result.Turns.Last().Candidates);
        Assert.Equal("Zlatna dolina", candidate.Label);
        Assert.Equal("Zlatna dolina", candidate.Text);
    }

    [Fact]
    public async Task A_fully_resolved_command_has_no_candidates_on_the_response()
    {
        AiReturns(OneInspection);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        var result = await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "Pregledana košnica 2" });

        Assert.Empty(result.Turns.Last().Candidates);
    }

    [Fact]
    public async Task Answering_a_clarification_continues_the_same_session_and_the_card_carries_the_earlier_fields()
    {
        // First turn: the apiary is named but not found, so nothing resolves — honeyLevel/broodStatus
        // are still captured on the unresolved row rather than discarded.
        AiReturns("""
            { "reply": "Nisam prepoznao taj pčelinjak.", "actions": [
              { "kind": "create_inspection", "apiary": "Nepostojeći", "hives": ["2"],
                "fields": { "honeyLevel": 3, "broodStatus": "Matica uočena" } } ] }
            """);

        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service().StartSessionAsync(new StartAssistantSessionDto
        {
            Text = "Pregledana košnica 2 na pčelinjaku Nepostojeći, med puno, matica uočena",
        });

        // Second turn: the user taps "Zlatna dolina" (a candidate button sends its Text exactly like
        // typed input). Per the continuation prompt rule, the model re-emits the whole action —
        // mocked here, since only our own carry-through is testable without live Groq.
        AiReturns("""
            { "reply": "Unosim pregled.", "actions": [
              { "kind": "create_inspection", "apiary": "Zlatna dolina", "hives": ["2"],
                "fields": { "honeyLevel": 3, "broodStatus": "Matica uočena" } } ] }
            """);

        var result = await Service().AddTurnAsync(saved!.Id, new AssistantTurnRequestDto { Text = "Zlatna dolina" });

        Assert.Equal(saved.Id, result.Id); // same session — a continuation, not a new thread
        var action = Assert.Single(result.Turns.Last().Actions);
        Assert.Equal("Zlatna dolina · Košnica 2", action.TargetSummary);
        Assert.Equal(HoneyLevel.High, action.Fields.HoneyLevel);
        Assert.Equal("Matica uočena", action.Fields.BroodStatus);
        Assert.Empty(result.Turns.Last().Candidates); // resolved now — no lingering question
    }

    [Fact]
    public async Task Only_the_latest_turn_keeps_its_candidates_once_a_newer_turn_exists()
    {
        AiReturns("""
            { "reply": "Nisam prepoznao taj pčelinjak.", "actions": [
              { "kind": "create_inspection", "apiary": "Nepostojeći", "hives": ["2"], "fields": {} } ] }
            """);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "Pregledana košnica 2 na pčelinjaku Nepostojeći" });

        // A second, unrelated question — the first turn's buttons must no longer be answerable.
        AiReturns("""
            { "reply": "Ni ovaj pčelinjak nisam prepoznao.", "actions": [
              { "kind": "create_inspection", "apiary": "Drugi Nepostojeći", "hives": ["3"], "fields": {} } ] }
            """);
        var result = await Service().AddTurnAsync(saved!.Id, new AssistantTurnRequestDto { Text = "još nešto" });

        var assistantTurns = result.Turns.Where(t => t.Role == "Assistant").ToList();
        Assert.Equal(2, assistantTurns.Count);
        Assert.Empty(assistantTurns[0].Candidates);    // the earlier question, now superseded
        Assert.NotEmpty(assistantTurns[1].Candidates); // only the newest one is still live
    }

    [Fact]
    public async Task Over_the_ceiling_the_reply_states_the_real_count_and_offers_nothing()
    {
        var manyHives = Enumerable.Range(1, 60)
            .Select(i => new Beehive { Id = 100 + i, Name = $"Košnica {i}", LabelNumber = i.ToString(), ApiaryId = 1 })
            .ToList();
        _access.GetAccessibleBeehivesAsync().Returns<IReadOnlyList<Beehive>>(manyHives);

        AiReturns("""
            { "reply": "ok", "actions": [
              { "kind": "create_inspection", "apiary": "Zlatna dolina", "hives": "all", "fields": {} } ] }
            """);

        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service(config: Config(("Ai:MaxActionsPerCommand", "50")))
            .StartSessionAsync(new StartAssistantSessionDto { Text = "pregled svih košnica" });

        var turn = saved!.Turns.Last();
        Assert.Empty(turn.Actions);
        Assert.Contains("60", turn.Content);
        Assert.Contains("50", turn.Content);
    }

    // ── Confirmation ──────────────────────────────────────────────────────────

    private AiAssistantTurn PendingTurn(int userId = 1, params AiAssistantAction[] actions)
    {
        var turn = new AiAssistantTurn
        {
            Id = 9,
            Role = AiTurnRole.Assistant,
            Session = new AiAssistantSession { Id = 5, UserId = userId },
        };
        foreach (var a in actions) turn.Actions.Add(a);
        _uow.AiAssistantSessions.GetTurnWithActionsAsync(9).Returns(turn);
        return turn;
    }

    private static AiAssistantAction PendingAction(int id, AiActionKind kind = AiActionKind.CreateInspection) => new()
    {
        Id = id,
        Kind = kind,
        Status = AiActionStatus.Pending,
        TargetSummary = "Zlatna dolina · Košnica 2",
        PayloadJson = new AiActionPayload(1, "Zlatna dolina", 11, "Košnica 2",
            AiResolutionIssue.None, [], new AiActionFields(HoneyLevel: HoneyLevel.Medium)).ToJson(),
    };

    [Fact]
    public async Task Confirming_executes_only_the_checked_actions_and_rejects_the_rest()
    {
        var turn = PendingTurn(actions: [PendingAction(1), PendingAction(2)]);
        _executor.ExecuteAsync(Arg.Any<AiActionKind>(), Arg.Any<AiActionPayload>())
            .Returns(new AiActionOutcome(true, "Inspection", 100, null));

        var response = await Service().ConfirmAsync(9, new ConfirmActionsDto
        {
            Actions = [new ConfirmActionItemDto { Id = 1 }],
        });

        Assert.Equal(AiActionStatus.Confirmed, turn.Actions[0].Status);
        Assert.Equal(100, turn.Actions[0].ResultEntityId);
        Assert.Equal(AiActionStatus.Rejected, turn.Actions[1].Status);
        Assert.Single(response.Results);
        await _executor.Received(1).ExecuteAsync(Arg.Any<AiActionKind>(), Arg.Any<AiActionPayload>());
    }

    [Fact]
    public async Task A_second_confirm_on_the_same_turn_is_refused_rather_than_creating_duplicates()
    {
        var turn = PendingTurn(actions: [PendingAction(1)]);
        _executor.ExecuteAsync(Arg.Any<AiActionKind>(), Arg.Any<AiActionPayload>())
            .Returns(new AiActionOutcome(true, "Inspection", 100, null));

        var request = new ConfirmActionsDto { Actions = [new ConfirmActionItemDto { Id = 1 }] };
        await Service().ConfirmAsync(9, request);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service().ConfirmAsync(9, request));

        // The double-tap must not reach the executor a second time.
        await _executor.Received(1).ExecuteAsync(Arg.Any<AiActionKind>(), Arg.Any<AiActionPayload>());
        Assert.Equal(100, turn.Actions[0].ResultEntityId);
    }

    [Fact]
    public async Task A_mid_batch_failure_is_reported_per_action_and_leaves_the_others_intact()
    {
        var turn = PendingTurn(actions: [PendingAction(1), PendingAction(2), PendingAction(3)]);

        var call = 0;
        _executor.ExecuteAsync(Arg.Any<AiActionKind>(), Arg.Any<AiActionPayload>())
            .Returns(_ => ++call == 2
                ? new AiActionOutcome(false, null, null, "Nemate ovlaštenje za ovu radnju.")
                : new AiActionOutcome(true, "Inspection", 100 + call, null));

        var response = await Service().ConfirmAsync(9, new ConfirmActionsDto
        {
            Actions = [new ConfirmActionItemDto { Id = 1 }, new ConfirmActionItemDto { Id = 2 }, new ConfirmActionItemDto { Id = 3 }],
        });

        Assert.Equal(AiActionStatus.Confirmed, turn.Actions[0].Status);
        Assert.Equal(AiActionStatus.Failed, turn.Actions[1].Status);
        Assert.Equal("Nemate ovlaštenje za ovu radnju.", turn.Actions[1].ErrorMessage);
        Assert.Equal(AiActionStatus.Confirmed, turn.Actions[2].Status);

        // The summary must not claim a clean run.
        Assert.Contains("2 od 3", response.Message);
    }

    [Fact]
    public async Task An_edited_hive_id_is_re_checked_against_what_the_caller_can_actually_reach()
    {
        PendingTurn(actions: [PendingAction(1)]);

        // The client edits the card to a hive that is not in the caller's accessible set.
        var response = await Service().ConfirmAsync(9, new ConfirmActionsDto
        {
            Actions = [new ConfirmActionItemDto { Id = 1, BeehiveId = 999 }],
        });

        await _executor.DidNotReceive().ExecuteAsync(Arg.Any<AiActionKind>(), Arg.Any<AiActionPayload>());
        Assert.Equal(nameof(AiActionStatus.Failed), response.Results[0].Status);
    }

    [Fact]
    public async Task An_edited_field_reaches_the_executor_instead_of_the_proposed_value()
    {
        PendingTurn(actions: [PendingAction(1)]);
        AiActionPayload? executed = null;
        _executor.ExecuteAsync(Arg.Any<AiActionKind>(), Arg.Do<AiActionPayload>(p => executed = p))
            .Returns(new AiActionOutcome(true, "Inspection", 100, null));

        await Service().ConfirmAsync(9, new ConfirmActionsDto
        {
            Actions =
            [
                new ConfirmActionItemDto
                {
                    Id = 1,
                    BeehiveId = 11,
                    Fields = new AssistantFieldsDto { HoneyLevel = HoneyLevel.High, Notes = "ispravljeno" },
                },
            ],
        });

        Assert.Equal(HoneyLevel.High, executed!.Fields.HoneyLevel);
        Assert.Equal("ispravljeno", executed.Fields.Notes);
        Assert.Equal(11, executed.BeehiveId);
    }

    [Fact]
    public async Task Confirming_nothing_rejects_everything_without_executing()
    {
        var turn = PendingTurn(actions: [PendingAction(1), PendingAction(2)]);

        var response = await Service().ConfirmAsync(9, new ConfirmActionsDto());

        Assert.All(turn.Actions, a => Assert.Equal(AiActionStatus.Rejected, a.Status));
        Assert.Empty(response.Results);
        await _executor.DidNotReceive().ExecuteAsync(Arg.Any<AiActionKind>(), Arg.Any<AiActionPayload>());
    }

    [Fact]
    public async Task Rejecting_marks_every_pending_proposal_and_writes_no_records()
    {
        var turn = PendingTurn(actions: [PendingAction(1), PendingAction(2)]);

        await Service().RejectAsync(9);

        Assert.All(turn.Actions, a => Assert.Equal(AiActionStatus.Rejected, a.Status));
        await _executor.DidNotReceive().ExecuteAsync(Arg.Any<AiActionKind>(), Arg.Any<AiActionPayload>());
    }

    // ── Session limits & transcription ────────────────────────────────────────

    [Fact]
    public async Task A_session_at_the_turn_cap_refuses_before_calling_the_model()
    {
        var session = new AiAssistantSession { Id = 5, UserId = 1 };
        for (var i = 0; i < 40; i++)
            session.Turns.Add(new AiAssistantTurn { Role = AiTurnRole.User, Content = "x" });
        _uow.AiAssistantSessions.GetWithTurnsAsync(5).Returns(session);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().AddTurnAsync(5, new AssistantTurnRequestDto { Text = "još" }));

        await _ai.DidNotReceive().SendAsync(Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_empty_transcript_is_a_validation_error_not_a_blank_command()
    {
        _transcription.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string>()).Returns("   ");

        await Assert.ThrowsAsync<ValidationException>(
            () => Service().TranscribeAsync(new MemoryStream(), "a.webm"));
    }

    [Fact]
    public async Task Transcription_is_gated_as_voice_input_and_does_not_consume_a_command()
    {
        _transcription.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string>()).Returns("pregledana košnica dva");

        var transcript = await Service().TranscribeAsync(new MemoryStream(), "a.webm");

        Assert.Equal("pregledana košnica dva", transcript);
        await _plan.Received(1).EnsureFeatureAsync(7, PlanFeature.VoiceInput);
        await _plan.DidNotReceive().EnsureAiInteractionAsync(Arg.Any<int>());
    }

    // ── Phase C: existing-record pipeline ────────────────────────────────────

    private static readonly TodoDto Postolje = new()
    {
        Id = 501, Title = "Dodaj postolje", Notes = "stara napomena",
        DueDate = new DateTime(2026, 8, 20), Priority = TodoPriority.Low,
        IsCompleted = false, BeehiveId = 11,
    };

    [Fact]
    public async Task A_resolved_update_todo_action_is_marked_destructive_with_the_current_values()
    {
        _todos.GetAllOpenForCurrentUserAsync().Returns(new[] { Postolje });
        AiReturns("""
            { "reply": "ok", "actions": [
              { "kind": "update_todo", "targetTitle": "Dodaj postolje",
                "fields": { "priority": 3 } } ] }
            """);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        var result = await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "promijeni prioritet zadatka Dodaj postolje na visok" });

        var action = Assert.Single(result.Turns.Last().Actions);
        Assert.True(action.IsDestructive);
        Assert.Equal("Dodaj postolje", action.PreviousFields?.Title);
        Assert.Equal(TodoPriority.Low, action.PreviousFields?.Priority);
        Assert.Equal("Dodaj postolje", action.TargetSummary);
    }

    [Fact]
    public async Task A_resolved_create_action_is_never_marked_destructive()
    {
        AiReturns(OneInspection);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        var result = await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "Pregledana košnica 2" });

        Assert.False(result.Turns.Last().Actions[0].IsDestructive);
    }

    [Fact]
    public async Task A_resolved_complete_todo_action_is_never_marked_destructive()
    {
        // Completing is a one-tap, reversible toggle everywhere else in the app — SPEC-17 D5 says
        // "update and delete", not "complete".
        _todos.GetAllOpenForCurrentUserAsync().Returns(new[] { Postolje });
        AiReturns("""
            { "reply": "ok", "actions": [
              { "kind": "complete_todo", "targetTitle": "Dodaj postolje", "fields": {} } ] }
            """);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        var result = await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "završio sam zadatak Dodaj postolje" });

        Assert.False(result.Turns.Last().Actions[0].IsDestructive);
    }

    [Fact]
    public async Task The_todo_pool_is_not_fetched_for_a_plain_create_command()
    {
        AiReturns(OneInspection);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "Pregledana košnica 2" });

        await _todos.DidNotReceive().GetAllOpenForCurrentUserAsync();
    }

    [Fact]
    public async Task The_inspection_pool_is_not_fetched_unless_an_action_needs_it()
    {
        AiReturns(OneInspection); // create_inspection — not update/delete
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "Pregledana košnica 2" });

        await _inspections.DidNotReceive().GetByBeehiveIdAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task Confirming_an_update_ignores_a_client_supplied_target_and_keeps_the_resolved_record()
    {
        _todos.GetAllOpenForCurrentUserAsync().Returns(new[] { Postolje });
        _executor.ExecuteAsync(AiActionKind.UpdateTodo, Arg.Any<AiActionPayload>())
            .Returns(new AiActionOutcome(true, "Todo", 501, null));

        var turn = new AiAssistantTurn
        {
            Id = 9,
            Session = new AiAssistantSession { Id = 5, UserId = 1 },
            Actions =
            {
                new AiAssistantAction
                {
                    Id = 1, Kind = AiActionKind.UpdateTodo, Status = AiActionStatus.Pending,
                    TargetSummary = "Dodaj postolje",
                    PayloadJson = new AiActionPayload(
                        1, "Zlatna dolina", 11, "Košnica 2", AiResolutionIssue.None, [], new AiActionFields(),
                        501, "Todo", "Dodaj postolje").ToJson(),
                },
            },
        };
        _uow.AiAssistantSessions.GetTurnWithActionsAsync(9).Returns(turn);

        // The client tries to redirect the update at a different hive — Phase C targets are fixed at
        // propose time and are never re-picked from the confirm request (SPEC-17 §5.3).
        await Service().ConfirmAsync(9, new ConfirmActionsDto
        {
            Actions = [new ConfirmActionItemDto { Id = 1, BeehiveId = 999, Fields = new AssistantFieldsDto() }],
        });

        await _executor.Received(1).ExecuteAsync(AiActionKind.UpdateTodo,
            Arg.Is<AiActionPayload>(p => p.ExistingEntityId == 501 && p.BeehiveId == 11));
    }

    [Fact]
    public async Task Confirming_a_delete_calls_the_executor_with_the_resolved_entity_id()
    {
        _executor.ExecuteAsync(AiActionKind.DeleteTodo, Arg.Any<AiActionPayload>())
            .Returns(new AiActionOutcome(true, "Todo", 501, null));

        var turn = new AiAssistantTurn
        {
            Id = 9,
            Session = new AiAssistantSession { Id = 5, UserId = 1 },
            Actions =
            {
                new AiAssistantAction
                {
                    Id = 1, Kind = AiActionKind.DeleteTodo, Status = AiActionStatus.Pending,
                    TargetSummary = "Dodaj postolje",
                    PayloadJson = new AiActionPayload(
                        1, "Zlatna dolina", 11, "Košnica 2", AiResolutionIssue.None, [], new AiActionFields(),
                        501, "Todo", "Dodaj postolje").ToJson(),
                },
            },
        };
        _uow.AiAssistantSessions.GetTurnWithActionsAsync(9).Returns(turn);

        var response = await Service().ConfirmAsync(9, new ConfirmActionsDto
        {
            Actions = [new ConfirmActionItemDto { Id = 1, Fields = new AssistantFieldsDto() }],
        });

        Assert.Equal(nameof(AiActionStatus.Confirmed), response.Results[0].Status);
        Assert.Equal(501, response.Results[0].ResultEntityId);
    }

    // ── Q&A (SPEC-18) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_pure_question_with_no_actions_renders_as_a_plain_answer_with_no_cards()
    {
        AiReturns("""
            { "reply": "Med se vrca kad je satje zapečaćeno bar dvije trećine.", "actions": [] }
            """);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        var result = await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "Kad se vrca med?" });

        var answer = result.Turns.Last();
        Assert.Empty(answer.Actions);
        Assert.Empty(answer.Candidates);
        Assert.Equal("Med se vrca kad je satje zapečaćeno bar dvije trećine.", answer.Content);
    }

    [Fact]
    public async Task Context_is_omitted_when_no_beehive_is_in_scope()
    {
        IReadOnlyList<ChatMessage>? sentChat = null;
        _ai.SendAsync(Arg.Do<IReadOnlyList<ChatMessage>>(c => sentChat = c), Arg.Any<CancellationToken>())
            .Returns(OneInspection);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "Pregledana košnica 2" });

        Assert.DoesNotContain("PODACI O KOŠNICI", sentChat![0].Content);
        await _uow.Beehives.DidNotReceive().GetByIdAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task Context_is_built_when_an_accessible_beehive_is_in_scope()
    {
        WireEmptyHiveData(beehiveId: 11);

        IReadOnlyList<ChatMessage>? sentChat = null;
        _ai.SendAsync(Arg.Do<IReadOnlyList<ChatMessage>>(c => sentChat = c), Arg.Any<CancellationToken>())
            .Returns(OneInspection);
        AiAssistantSession? saved = null;
        await _uow.AiAssistantSessions.AddAsync(Arg.Do<AiAssistantSession>(s => saved = s));
        _uow.AiAssistantSessions.GetWithTurnsAsync(Arg.Any<int>()).Returns(_ => saved);

        await Service().StartSessionAsync(new StartAssistantSessionDto { Text = "Pregledana košnica 2", BeehiveId = 11 });

        Assert.Contains("PODACI O KOŠNICI", sentChat![0].Content);
        Assert.Equal(11, saved!.BeehiveId); // the session itself remembers the hive, not just the turn
        await _access.Received(1).EnsureCanAccessBeehiveAsync(11);
    }

    [Fact]
    public async Task An_inaccessible_beehive_at_session_start_is_rejected_before_calling_the_model()
    {
        _access.EnsureCanAccessBeehiveAsync(7).Returns(Task.FromException(new ForbiddenAccessException()));

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => Service().StartSessionAsync(new StartAssistantSessionDto { Text = "x", BeehiveId = 7 }));

        await _ai.DidNotReceive().SendAsync(Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<CancellationToken>());
        await _uow.AiAssistantSessions.DidNotReceive().AddAsync(Arg.Any<AiAssistantSession>());
    }

    [Fact]
    public async Task A_revoked_hive_on_a_later_turn_drops_context_silently_instead_of_failing()
    {
        // The session was bound to a hive on an earlier turn; access to it was withdrawn since.
        var session = new AiAssistantSession { Id = 5, UserId = 1, BeehiveId = 11 };
        _uow.AiAssistantSessions.GetWithTurnsAsync(5).Returns(session);
        _access.CanAccessBeehiveAsync(11).Returns(false);

        IReadOnlyList<ChatMessage>? sentChat = null;
        _ai.SendAsync(Arg.Do<IReadOnlyList<ChatMessage>>(c => sentChat = c), Arg.Any<CancellationToken>())
            .Returns(OneInspection);

        var result = await Service().AddTurnAsync(5, new AssistantTurnRequestDto { Text = "još nešto" });

        Assert.DoesNotContain("PODACI O KOŠNICI", sentChat![0].Content);
        Assert.NotEmpty(result.Turns); // the turn still succeeds — it just answers without grounding
        await _uow.Beehives.DidNotReceive().GetByIdAsync(Arg.Any<int>());
    }
}
