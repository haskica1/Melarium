using Melarium.Application.Features.Assistant;
using Melarium.Domain.Enums;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Resolution is what decides which real hive a spoken sentence touches (SPEC-17 §4). The invariant
/// under test throughout: the resolver only ever sees what <c>IAccessGuard</c> handed it, so a hive
/// outside the caller's scope cannot resolve — and it never guesses when a name is ambiguous.
/// </summary>
public class AiTargetResolverTests
{
    private static readonly ApiaryRef Zlatna = new(1, "Zlatna dolina");
    private static readonly ApiaryRef Livada = new(2, "Livada");

    private static AiResolutionContext Context(
        IReadOnlyList<ApiaryRef>? apiaries = null,
        IReadOnlyList<HiveRef>? hives = null,
        int? pageApiaryId = null,
        int? pageBeehiveId = null,
        int maxTargets = 50,
        IReadOnlyList<TodoRef>? todos = null,
        IReadOnlyList<InspectionRef>? inspections = null) =>
        new(apiaries ?? [Zlatna, Livada],
            hives ?? [new HiveRef(10, "Košnica 1", "1", 1), new HiveRef(11, "Košnica 2", "2", 1)],
            pageApiaryId, pageBeehiveId, maxTargets, todos, inspections);

    private static AiEnvelope Envelope(params AiProposedAction[] actions) => new("", null, actions);

    private static AiProposedAction Inspection(string? apiary, AiHiveSelector hives) =>
        new(AiActionKind.CreateInspection, apiary, hives, new AiActionFields());

    private static AiProposedAction Todo(string? apiary, AiHiveSelector hives, string? title = "Dodaj postolje") =>
        new(AiActionKind.CreateTodo, apiary, hives, new AiActionFields(Title: title));

    private static AiHiveSelector Numbers(params string[] n) => new(false, n);
    private static readonly AiHiveSelector All = new(true, []);
    private static readonly AiHiveSelector NoHives = AiHiveSelector.None;

    // ── Phase C action builders ──────────────────────────────────────────────

    private static AiProposedAction UpdateTodoAction(string? apiary, AiHiveSelector hives, string? targetTitle) =>
        new(AiActionKind.UpdateTodo, apiary, hives, new AiActionFields(Notes: "izmijenjeno"), targetTitle);

    private static AiProposedAction CompleteTodoAction(string? apiary, AiHiveSelector hives, string? targetTitle) =>
        new(AiActionKind.CompleteTodo, apiary, hives, new AiActionFields(), targetTitle);

    private static AiProposedAction DeleteTodoAction(string? apiary, AiHiveSelector hives, string? targetTitle) =>
        new(AiActionKind.DeleteTodo, apiary, hives, new AiActionFields(), targetTitle);

    private static AiProposedAction UpdateInspectionAction(string? apiary, AiHiveSelector hives, DateOnly? date = null) =>
        new(AiActionKind.UpdateInspection, apiary, hives, new AiActionFields(Date: date, HoneyLevel: HoneyLevel.High));

    private static AiProposedAction DeleteInspectionAction(string? apiary, AiHiveSelector hives, DateOnly? date = null) =>
        new(AiActionKind.DeleteInspection, apiary, hives, new AiActionFields(Date: date));

    // ── Apiary name matching ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Zlatna dolina")]
    [InlineData("zlatna dolina")]
    [InlineData("ZLATNA DOLINA")]
    [InlineData("  Zlatna   dolina ")]
    [InlineData("Zlatna")]              // partial
    [InlineData("pčelinjak Zlatna dolina")] // the query contains the name
    public void Apiary_matches_case_whitespace_and_partials(string spoken)
    {
        var result = AiTargetResolver.Resolve(Envelope(Inspection(spoken, Numbers("2"))), Context());

        var action = Assert.Single(result.Actions);
        Assert.Equal(AiResolutionIssue.None, action.Issue);
        Assert.Equal(1, Assert.Single(action.Targets).ApiaryId);
    }

    [Fact]
    public void Diacritics_are_folded_because_speech_to_text_drops_them_constantly()
    {
        var apiaries = new[] { new ApiaryRef(7, "Šumska čistina"), Livada };
        var hives = new[] { new HiveRef(70, "Košnica 3", "3", 7) };

        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("sumska cistina", Numbers("3"))),
            Context(apiaries, hives));

        Assert.Equal(7, Assert.Single(result.Actions[0].Targets).ApiaryId);
    }

    [Fact]
    public void Dj_is_folded_too_even_though_Unicode_does_not_decompose_it()
    {
        Assert.Equal("djurdjevak", AiTargetResolver.Normalize("Đurđevak"));
        Assert.Equal("medjugorje", AiTargetResolver.Normalize("Međugorje"));
    }

    [Fact]
    public void Unknown_apiary_yields_not_found_with_the_reachable_ones_as_candidates()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Nepostojeći pčelinjak", Numbers("2"))), Context());

        var action = result.Actions[0];
        Assert.Equal(AiResolutionIssue.ApiaryNotFound, action.Issue);
        Assert.Empty(action.Targets);
        Assert.Equal(2, action.ApiaryCandidateList.Count);
    }

    [Fact]
    public void Two_apiaries_matching_the_same_words_are_ambiguous_never_a_guess()
    {
        var apiaries = new[] { new ApiaryRef(1, "Gornja livada"), new ApiaryRef(2, "Donja livada") };

        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("livada", Numbers("1"))),
            Context(apiaries, [new HiveRef(10, "Košnica 1", "1", 1)]));

        Assert.Equal(AiResolutionIssue.ApiaryAmbiguous, result.Actions[0].Issue);
        Assert.Empty(result.Actions[0].Targets);
    }

    [Fact]
    public void A_single_apiary_is_assumed_when_none_was_named()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(Todo(null, AiHiveSelector.None)),
            Context([Zlatna], []));

        var target = Assert.Single(result.Actions[0].Targets);
        Assert.Equal(1, target.ApiaryId);
        Assert.Null(target.BeehiveId);
    }

    // ── Hive number matching ──────────────────────────────────────────────────

    [Theory]
    [InlineData("2")]
    [InlineData("02")]
    [InlineData("002")]
    public void Hive_numbers_match_regardless_of_leading_zeros(string spoken)
    {
        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Zlatna dolina", Numbers(spoken))), Context());

        Assert.Equal(11, Assert.Single(result.Actions[0].Targets).BeehiveId);
    }

    [Fact]
    public void A_hive_without_a_label_number_still_matches_on_the_digits_in_its_name()
    {
        var hives = new[] { new HiveRef(20, "Grupa 2 - košnica 5", null, 1) };

        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Zlatna dolina", Numbers("5"))),
            Context([Zlatna], hives));

        Assert.Equal(20, Assert.Single(result.Actions[0].Targets).BeehiveId);
    }

    [Fact]
    public void Several_numbers_become_several_targets()
    {
        var hives = new[]
        {
            new HiveRef(10, "Košnica 1", "1", 1),
            new HiveRef(11, "Košnica 2", "2", 1),
            new HiveRef(12, "Košnica 3", "3", 1),
        };

        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Zlatna dolina", Numbers("1", "2", "3"))),
            Context([Zlatna], hives));

        Assert.Equal(AiResolutionIssue.None, result.Actions[0].Issue);
        Assert.Equal([10, 11, 12], result.Actions[0].Targets.Select(t => t.BeehiveId));
    }

    [Fact]
    public void Partly_unknown_numbers_still_produce_the_cards_that_did_resolve()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Zlatna dolina", Numbers("1", "7"))), Context());

        var action = result.Actions[0];
        Assert.Equal(10, Assert.Single(action.Targets).BeehiveId);
        Assert.Equal(AiResolutionIssue.HiveNotFound, action.Issue);
        Assert.Equal(["7"], action.UnresolvedNumbers);
    }

    [Fact]
    public void The_same_number_on_two_apiaries_is_ambiguous_and_carries_the_candidates_out()
    {
        var hives = new[]
        {
            new HiveRef(11, "Košnica 2", "2", 1),
            new HiveRef(21, "Košnica 2", "2", 2),
        };

        var result = AiTargetResolver.Resolve(
            Envelope(Inspection(null, Numbers("2"))),
            Context([Zlatna, Livada], hives));

        var action = result.Actions[0];
        Assert.Equal(AiResolutionIssue.HiveAmbiguous, action.Issue);
        Assert.Empty(action.Targets);
        Assert.Equal(2, action.HiveCandidateList.Count);
    }

    [Fact]
    public void Naming_the_apiary_disambiguates_the_same_number()
    {
        var hives = new[]
        {
            new HiveRef(11, "Košnica 2", "2", 1),
            new HiveRef(21, "Košnica 2", "2", 2),
        };

        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Livada", Numbers("2"))),
            Context([Zlatna, Livada], hives));

        Assert.Equal(21, Assert.Single(result.Actions[0].Targets).BeehiveId);
    }

    // ── The invariant ─────────────────────────────────────────────────────────

    [Fact]
    public void A_hive_outside_the_callers_scope_cannot_be_reached_by_any_phrasing()
    {
        // The Beekeeper is assigned only hive 10, so IAccessGuard hands over only that one.
        var reachable = new[] { new HiveRef(10, "Košnica 1", "1", 1) };

        foreach (var phrasing in new[] { Numbers("2"), Numbers("02"), All })
        {
            var result = AiTargetResolver.Resolve(
                Envelope(Inspection("Zlatna dolina", phrasing)),
                Context([Zlatna], reachable));

            Assert.DoesNotContain(result.Actions[0].Targets, t => t.BeehiveId == 11);
        }
    }

    // ── "sve košnice" ─────────────────────────────────────────────────────────

    [Fact]
    public void All_expands_to_every_reachable_hive_on_the_named_apiary()
    {
        var hives = new[]
        {
            new HiveRef(10, "Košnica 1", "1", 1),
            new HiveRef(11, "Košnica 2", "2", 1),
            new HiveRef(21, "Košnica 9", "9", 2), // another apiary — must not be swept in
        };

        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Zlatna dolina", All)),
            Context([Zlatna, Livada], hives));

        Assert.Equal([10, 11], result.Actions[0].Targets.Select(t => t.BeehiveId));
    }

    [Fact]
    public void All_without_a_named_apiary_is_ambiguous_not_a_wildcard_over_the_organization()
    {
        var result = AiTargetResolver.Resolve(Envelope(Inspection(null, All)), Context());

        Assert.Equal(AiResolutionIssue.ApiaryAmbiguous, result.Actions[0].Issue);
        Assert.Empty(result.Actions[0].Targets);
    }

    [Fact]
    public void All_on_an_empty_apiary_reports_that_rather_than_succeeding_silently()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Livada", All)),
            Context([Zlatna, Livada], [new HiveRef(10, "Košnica 1", "1", 1)]));

        Assert.Equal(AiResolutionIssue.NoHivesInApiary, result.Actions[0].Issue);
    }

    // ── Ceiling ───────────────────────────────────────────────────────────────

    [Fact]
    public void Over_the_ceiling_nothing_is_offered_and_the_real_count_is_reported()
    {
        var hives = Enumerable.Range(1, 60)
            .Select(i => new HiveRef(100 + i, $"Košnica {i}", i.ToString(), 1))
            .ToList();

        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Zlatna dolina", All)),
            Context([Zlatna], hives, maxTargets: 50));

        Assert.True(result.ExceededCeiling);
        Assert.Equal(60, result.TotalTargets);
        Assert.Empty(result.Actions[0].Targets);
    }

    [Fact]
    public void The_ceiling_counts_the_whole_envelope_not_one_action()
    {
        var hives = Enumerable.Range(1, 30)
            .Select(i => new HiveRef(100 + i, $"Košnica {i}", i.ToString(), 1))
            .ToList();

        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Zlatna dolina", All), Todo("Zlatna dolina", All)),
            Context([Zlatna], hives, maxTargets: 50));

        Assert.True(result.ExceededCeiling);
        Assert.Equal(60, result.TotalTargets);
    }

    // ── Page context ──────────────────────────────────────────────────────────

    [Fact]
    public void Page_context_fills_a_gap()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(Inspection(null, AiHiveSelector.None)),
            Context(pageBeehiveId: 11));

        Assert.Equal(11, Assert.Single(result.Actions[0].Targets).BeehiveId);
    }

    [Fact]
    public void A_spoken_hive_beats_the_page_the_user_is_standing_on()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Zlatna dolina", Numbers("1"))),
            Context(pageBeehiveId: 11));

        Assert.Equal(10, Assert.Single(result.Actions[0].Targets).BeehiveId);
    }

    [Fact]
    public void A_spoken_apiary_beats_the_page_context_too()
    {
        var hives = new[] { new HiveRef(11, "Košnica 2", "2", 1), new HiveRef(21, "Košnica 2", "2", 2) };

        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Livada", Numbers("2"))),
            Context([Zlatna, Livada], hives, pageApiaryId: 1));

        Assert.Equal(21, Assert.Single(result.Actions[0].Targets).BeehiveId);
    }

    // ── Per-kind rules ────────────────────────────────────────────────────────

    [Fact]
    public void An_inspection_without_a_hive_reports_the_missing_hive()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Zlatna dolina", AiHiveSelector.None)), Context());

        Assert.Equal(AiResolutionIssue.MissingHive, result.Actions[0].Issue);
        Assert.Empty(result.Actions[0].Targets);
    }

    [Fact]
    public void A_todo_without_a_hive_becomes_an_apiary_level_todo()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(Todo("Zlatna dolina", AiHiveSelector.None)), Context());

        var target = Assert.Single(result.Actions[0].Targets);
        Assert.Equal(1, target.ApiaryId);
        Assert.Null(target.BeehiveId);
        Assert.Equal("Zlatna dolina", target.Summary);
    }

    [Fact]
    public void A_todo_without_a_title_is_refused_before_anything_else_is_resolved()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(Todo("Zlatna dolina", Numbers("2"), title: null)), Context());

        Assert.Equal(AiResolutionIssue.MissingTitle, result.Actions[0].Issue);
        Assert.Empty(result.Actions[0].Targets);
    }

    [Fact]
    public void The_summary_names_both_the_apiary_and_the_hive()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(Inspection("Zlatna dolina", Numbers("2"))), Context());

        Assert.Equal("Zlatna dolina · Košnica 2", result.Actions[0].Targets[0].Summary);
    }

    // ── Phase C: existing todos, by title ────────────────────────────────────

    private static readonly TodoRef Postolje = new(
        501, "Dodaj postolje", "napomena", TodoPriority.Medium, new DateTime(2026, 8, 20), false, null, 11);

    [Fact]
    public void An_exact_title_match_resolves_to_the_existing_todo()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(CompleteTodoAction(null, NoHives, "Dodaj postolje")),
            Context(todos: [Postolje]));

        var target = Assert.Single(result.Actions[0].Targets);
        Assert.Equal(501, target.ExistingEntityId);
        Assert.Equal("Todo", target.ExistingEntityType);
        Assert.Equal("Dodaj postolje", target.Summary);
        Assert.Equal(AiResolutionIssue.None, result.Actions[0].Issue);
    }

    [Fact]
    public void A_partial_title_match_resolves_when_it_is_the_only_one()
    {
        var longer = Postolje with { Title = "Dodaj postolje za košnicu 2" };

        var result = AiTargetResolver.Resolve(
            Envelope(CompleteTodoAction(null, NoHives, "postolje")),
            Context(todos: [longer]));

        Assert.Equal(501, result.Actions[0].Targets[0].ExistingEntityId);
    }

    [Fact]
    public void No_matching_todo_title_reports_TodoNotFound()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(CompleteTodoAction(null, NoHives, "Ne postoji")),
            Context(todos: [Postolje]));

        Assert.Equal(AiResolutionIssue.TodoNotFound, result.Actions[0].Issue);
        Assert.Empty(result.Actions[0].Targets);
    }

    [Fact]
    public void Two_todos_sharing_a_title_are_ambiguous_never_a_guess()
    {
        var onHive2 = Postolje;
        var onOtherApiary = Postolje with { Id = 502, BeehiveId = null, ApiaryId = 2 };

        var result = AiTargetResolver.Resolve(
            Envelope(CompleteTodoAction(null, NoHives, "Dodaj postolje")),
            Context(todos: [onHive2, onOtherApiary]));

        Assert.Equal(AiResolutionIssue.TodoAmbiguous, result.Actions[0].Issue);
        Assert.Empty(result.Actions[0].Targets);
        Assert.Equal(2, result.Actions[0].TodoCandidateList.Count);
    }

    [Fact]
    public void Naming_the_hive_narrows_a_duplicate_title_to_the_right_todo()
    {
        var onHive2 = Postolje;
        var onHive1 = Postolje with { Id = 502, BeehiveId = 10 };

        var result = AiTargetResolver.Resolve(
            Envelope(CompleteTodoAction("Zlatna dolina", Numbers("1"), "Dodaj postolje")),
            Context(todos: [onHive2, onHive1]));

        Assert.Equal(502, Assert.Single(result.Actions[0].Targets).ExistingEntityId);
    }

    [Fact]
    public void Naming_the_apiary_narrows_the_todo_search_to_its_own_hives()
    {
        var onZlatna = Postolje; // BeehiveId 11 is on Zlatna dolina (apiary 1)
        var onLivada = Postolje with { Id = 502, BeehiveId = null, ApiaryId = 2 };

        var result = AiTargetResolver.Resolve(
            Envelope(CompleteTodoAction("Livada", NoHives, "Dodaj postolje")),
            Context(todos: [onZlatna, onLivada]));

        Assert.Equal(502, Assert.Single(result.Actions[0].Targets).ExistingEntityId);
    }

    [Fact]
    public void A_missing_target_title_is_refused_before_any_search()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(DeleteTodoAction(null, NoHives, targetTitle: null)),
            Context(todos: [Postolje]));

        Assert.Equal(AiResolutionIssue.MissingTitle, result.Actions[0].Issue);
    }

    [Fact]
    public void A_todo_outside_the_supplied_pool_never_resolves()
    {
        // The pool is what AiAssistantService populates from the caller's own accessible todos — an
        // empty pool here stands in for "not accessible", the same invariant §1 states for hives.
        var result = AiTargetResolver.Resolve(
            Envelope(DeleteTodoAction(null, NoHives, "Dodaj postolje")),
            Context(todos: []));

        Assert.Equal(AiResolutionIssue.TodoNotFound, result.Actions[0].Issue);
    }

    [Fact]
    public void UpdateTodo_carries_the_existing_titles_notes_and_due_date_as_PreviousFields()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(UpdateTodoAction(null, NoHives, "Dodaj postolje")),
            Context(todos: [Postolje]));

        var target = Assert.Single(result.Actions[0].Targets);
        Assert.Equal("Dodaj postolje", target.PreviousFields?.Title);
        Assert.Equal("napomena", target.PreviousFields?.Notes);
        Assert.Equal(new DateOnly(2026, 8, 20), target.PreviousFields?.DueDate);
    }

    // ── Phase C: existing inspections, by hive + date ────────────────────────

    private static readonly InspectionRef AugustFifth = new(
        601, new DateTime(2026, 8, 5), HoneyLevel.Medium, "Matica uočena", "napomena", 11);
    private static readonly InspectionRef AugustFirst = new(
        602, new DateTime(2026, 8, 1), HoneyLevel.Low, null, null, 11);

    [Fact]
    public void An_exact_date_resolves_to_that_inspection()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(UpdateInspectionAction("Zlatna dolina", Numbers("2"), new DateOnly(2026, 8, 5))),
            Context(inspections: [AugustFifth, AugustFirst]));

        var target = Assert.Single(result.Actions[0].Targets);
        Assert.Equal(601, target.ExistingEntityId);
        Assert.Equal("Inspection", target.ExistingEntityType);
    }

    [Fact]
    public void No_date_named_defaults_to_the_most_recent_inspection()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(DeleteInspectionAction("Zlatna dolina", Numbers("2"))),
            Context(inspections: [AugustFirst, AugustFifth])); // deliberately out of order

        Assert.Equal(601, Assert.Single(result.Actions[0].Targets).ExistingEntityId); // the later date
    }

    [Fact]
    public void A_date_with_no_matching_inspection_reports_InspectionNotFound()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(UpdateInspectionAction("Zlatna dolina", Numbers("2"), new DateOnly(2026, 1, 1))),
            Context(inspections: [AugustFifth]));

        Assert.Equal(AiResolutionIssue.InspectionNotFound, result.Actions[0].Issue);
    }

    [Fact]
    public void Two_inspections_on_the_same_named_date_are_ambiguous()
    {
        var second = AugustFifth with { Id = 603 };

        var result = AiTargetResolver.Resolve(
            Envelope(UpdateInspectionAction("Zlatna dolina", Numbers("2"), new DateOnly(2026, 8, 5))),
            Context(inspections: [AugustFifth, second]));

        Assert.Equal(AiResolutionIssue.InspectionAmbiguous, result.Actions[0].Issue);
        Assert.Equal(2, result.Actions[0].InspectionCandidateList.Count);
    }

    [Fact]
    public void An_inspection_action_without_exactly_one_hive_reports_MissingHive()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(DeleteInspectionAction("Zlatna dolina", NoHives)),
            Context(inspections: [AugustFifth]));

        Assert.Equal(AiResolutionIssue.MissingHive, result.Actions[0].Issue);
    }

    [Fact]
    public void DeleteInspection_carries_the_existing_values_as_PreviousFields()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(DeleteInspectionAction("Zlatna dolina", Numbers("2"), new DateOnly(2026, 8, 5))),
            Context(inspections: [AugustFifth]));

        var target = Assert.Single(result.Actions[0].Targets);
        Assert.Equal(HoneyLevel.Medium, target.PreviousFields?.HoneyLevel);
        Assert.Equal("Matica uočena", target.PreviousFields?.BroodStatus);
        Assert.Equal("napomena", target.PreviousFields?.Notes);
    }

    // ── Colony merge (SPEC-19 §8) ────────────────────────────────────────────

    private static AiProposedAction MergeAction(
        AiHiveSelector hives,
        string? targetHive = "2",
        MergeQueenOutcome? queen = MergeQueenOutcome.KeptTarget) =>
        new(AiActionKind.MergeBeehive, null, hives,
            new AiActionFields(TargetHive: targetHive, QueenOutcome: queen));

    [Fact]
    public void Merge_resolves_the_source_hive_as_the_target_and_the_receiving_hive_into_fields()
    {
        var result = AiTargetResolver.Resolve(Envelope(MergeAction(Numbers("1"), targetHive: "2")), Context());

        var action = result.Actions[0];
        var target = Assert.Single(action.Targets);

        // The resolved target is the hive that LEAVES; the receiving hive rides along in the fields.
        Assert.Equal(10, target.BeehiveId);
        Assert.Equal(11, action.Fields.TargetBeehiveId);
        Assert.Equal("Košnica 2", action.Fields.TargetBeehiveName);
        Assert.Equal(AiResolutionIssue.None, action.Issue);
    }

    [Fact]
    public void Merge_refuses_all_hives()
    {
        var result = AiTargetResolver.Resolve(Envelope(MergeAction(All)), Context());

        // Merging every hive into one on a misheard command would be unrecoverable.
        Assert.Equal(AiResolutionIssue.MergeSourceAmbiguous, result.Actions[0].Issue);
        Assert.Empty(result.Actions[0].Targets);
    }

    [Fact]
    public void Merge_refuses_more_than_one_source_hive()
    {
        var result = AiTargetResolver.Resolve(Envelope(MergeAction(Numbers("1", "2"))), Context());

        Assert.Equal(AiResolutionIssue.MergeSourceAmbiguous, result.Actions[0].Issue);
    }

    [Fact]
    public void Merge_without_a_receiving_hive_reports_it()
    {
        var result = AiTargetResolver.Resolve(Envelope(MergeAction(Numbers("1"), targetHive: null)), Context());

        Assert.Equal(AiResolutionIssue.MergeTargetNotFound, result.Actions[0].Issue);
        Assert.Empty(result.Actions[0].Targets);
    }

    [Fact]
    public void Merge_into_the_same_hive_reports_it()
    {
        var result = AiTargetResolver.Resolve(Envelope(MergeAction(Numbers("1"), targetHive: "1")), Context());

        Assert.Equal(AiResolutionIssue.MergeTargetNotFound, result.Actions[0].Issue);
    }

    [Fact]
    public void Merge_never_guesses_the_queen()
    {
        var result = AiTargetResolver.Resolve(Envelope(MergeAction(Numbers("1"), queen: null)), Context());

        var action = result.Actions[0];
        Assert.Equal(AiResolutionIssue.MissingQueenOutcome, action.Issue);
        Assert.Empty(action.Targets);
        // The receiving hive is still carried, so answering the question does not re-do the work.
        Assert.Equal(11, action.Fields.TargetBeehiveId);
    }

    [Fact]
    public void Merge_falls_back_to_the_hive_page_for_the_source()
    {
        var result = AiTargetResolver.Resolve(
            Envelope(MergeAction(NoHives, targetHive: "2")),
            Context(pageBeehiveId: 10));

        var target = Assert.Single(result.Actions[0].Targets);
        Assert.Equal(10, target.BeehiveId);
    }

    [Fact]
    public void Merge_cannot_reach_a_hive_outside_the_callers_scope()
    {
        // Only hive 10 is accessible; "2" is not in the context at all.
        var result = AiTargetResolver.Resolve(
            Envelope(MergeAction(Numbers("1"), targetHive: "2")),
            Context(hives: [new HiveRef(10, "Košnica 1", "1", 1)]));

        Assert.Equal(AiResolutionIssue.MergeTargetNotFound, result.Actions[0].Issue);
    }
}
