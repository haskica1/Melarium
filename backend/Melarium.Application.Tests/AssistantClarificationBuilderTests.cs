using Melarium.Application.Features.Assistant;
using Melarium.Domain.Enums;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks which unresolved bits become tappable buttons (SPEC-17 Phase B, D7). Most scenarios are driven
/// through the real <see cref="AiTargetResolver"/> first — the same pipeline <c>AiAssistantService</c>
/// uses — so this also proves the two pure stages compose correctly, not just the builder in isolation.
/// </summary>
public class AssistantClarificationBuilderTests
{
    private static readonly ApiaryRef Zlatna = new(1, "Zlatna dolina");
    private static readonly ApiaryRef Livada = new(2, "Livada");

    private static AiResolutionContext Context(
        IReadOnlyList<ApiaryRef>? apiaries = null, IReadOnlyList<HiveRef>? hives = null, int maxTargets = 50,
        IReadOnlyList<TodoRef>? todos = null, IReadOnlyList<InspectionRef>? inspections = null) =>
        new(apiaries ?? [Zlatna, Livada],
            hives ?? [new HiveRef(10, "Košnica 1", "1", 1), new HiveRef(11, "Košnica 2", "2", 1)],
            MaxTargets: maxTargets, Todos: todos, Inspections: inspections);

    private static AiEnvelope Envelope(AiClarification? needs = null, params AiProposedAction[] actions) =>
        new("", needs, actions);

    private static AiProposedAction Inspection(string? apiary, AiHiveSelector hives) =>
        new(AiActionKind.CreateInspection, apiary, hives, new AiActionFields());

    private static AiProposedAction CompleteTodoAction(string? targetTitle) =>
        new(AiActionKind.CompleteTodo, null, AiHiveSelector.None, new AiActionFields(), targetTitle);

    private static AiProposedAction DeleteInspectionAction(string apiary, AiHiveSelector hives, DateOnly date) =>
        new(AiActionKind.DeleteInspection, apiary, hives, new AiActionFields(Date: date));

    private static AiHiveSelector Numbers(params string[] n) => new(false, n);

    private static IReadOnlyList<AssistantCandidate> Build(AiEnvelope envelope, AiResolutionContext ctx)
    {
        var resolution = AiTargetResolver.Resolve(envelope, ctx);
        return AssistantClarificationBuilder.Build(envelope, resolution, ctx);
    }

    // ── The headline guarantee ───────────────────────────────────────────────

    [Fact]
    public void A_fully_resolved_command_produces_no_candidates()
    {
        var envelope = Envelope(actions: [Inspection("Zlatna dolina", Numbers("2"))]);

        Assert.Empty(Build(envelope, Context()));
    }

    // ── Apiary-level issues ──────────────────────────────────────────────────

    [Fact]
    public void Unknown_apiary_offers_every_reachable_apiary()
    {
        var envelope = Envelope(actions: [Inspection("Nepostojeći", Numbers("2"))]);

        var candidates = Build(envelope, Context());

        Assert.Equal(["Zlatna dolina", "Livada"], candidates.Select(c => c.Label));
        Assert.Equal(["Zlatna dolina", "Livada"], candidates.Select(c => c.Text));
    }

    [Fact]
    public void Ambiguous_apiary_name_offers_only_the_matches()
    {
        var apiaries = new[] { new ApiaryRef(1, "Gornja livada"), new ApiaryRef(2, "Donja livada") };
        var envelope = Envelope(actions: [Inspection("livada", Numbers("1"))]);

        var candidates = Build(envelope, Context(apiaries, [new HiveRef(10, "Košnica 1", "1", 1)]));

        Assert.Equal(["Gornja livada", "Donja livada"], candidates.Select(c => c.Label));
    }

    [Fact]
    public void More_than_eight_reachable_apiaries_is_too_many_buttons()
    {
        var many = Enumerable.Range(1, 9).Select(i => new ApiaryRef(i, $"Pčelinjak {i}")).ToArray();
        var envelope = Envelope(actions: [Inspection("Nepostojeći", Numbers("2"))]);

        Assert.Empty(Build(envelope, Context(many, [])));
    }

    // ── Hive-level issues ─────────────────────────────────────────────────────

    [Fact]
    public void Same_hive_number_on_two_apiaries_offers_the_apiaries_not_the_raw_hive_rows()
    {
        var hives = new[]
        {
            new HiveRef(11, "Košnica 2", "2", 1),
            new HiveRef(21, "Košnica 2", "2", 2),
        };
        var envelope = Envelope(actions: [Inspection(null, Numbers("2"))]);

        var candidates = Build(envelope, Context(hives: hives));

        Assert.Equal(["Zlatna dolina", "Livada"], candidates.Select(c => c.Label));
    }

    [Fact]
    public void Missing_hive_on_a_resolved_apiary_offers_its_hives()
    {
        var envelope = Envelope(actions: [Inspection("Zlatna dolina", AiHiveSelector.None)]);

        var candidates = Build(envelope, Context());

        Assert.Equal(["Košnica 1", "Košnica 2"], candidates.Select(c => c.Label));
        // The label NUMBER is what a beekeeper actually says, so it — not the display name — is sent.
        Assert.Equal(["1", "2"], candidates.Select(c => c.Text));
    }

    [Fact]
    public void A_hive_without_a_label_number_falls_back_to_its_name_as_the_sent_text()
    {
        var hives = new[] { new HiveRef(10, "Zadnja košnica", null, 1) };
        var envelope = Envelope(actions: [Inspection("Zlatna dolina", AiHiveSelector.None)]);

        var candidates = Build(envelope, Context(hives: hives));

        var only = Assert.Single(candidates);
        Assert.Equal("Zadnja košnica", only.Text);
    }

    [Fact]
    public void More_than_eight_hives_in_the_apiary_falls_back_to_the_cards_dropdown_instead()
    {
        var hives = Enumerable.Range(1, 9)
            .Select(i => new HiveRef(100 + i, $"Košnica {i}", i.ToString(), 1))
            .ToArray();
        var envelope = Envelope(actions: [Inspection("Zlatna dolina", AiHiveSelector.None)]);

        Assert.Empty(Build(envelope, Context(hives: hives)));
    }

    // ── Priority across a multi-action turn ──────────────────────────────────

    [Fact]
    public void An_earlier_resolved_action_does_not_hide_a_later_ambiguous_ones_candidates()
    {
        var envelope = Envelope(actions:
        [
            Inspection("Zlatna dolina", Numbers("1")),      // resolves cleanly, no candidates
            Inspection("Nepostojeći", Numbers("2")),         // this one needs help
        ]);

        var candidates = Build(envelope, Context());

        Assert.Equal(["Zlatna dolina", "Livada"], candidates.Select(c => c.Label));
    }

    // ── The model's own "needs", with no per-action candidates to draw on ────

    [Fact]
    public void A_bare_needs_apiary_question_offers_the_reachable_apiaries()
    {
        var envelope = Envelope(needs: new AiClarification("apiary", "Na kojem pčelinjaku?"));

        var candidates = Build(envelope, Context());

        Assert.Equal(["Zlatna dolina", "Livada"], candidates.Select(c => c.Label));
    }

    [Fact]
    public void A_bare_needs_beehive_question_offers_the_reachable_hives()
    {
        var envelope = Envelope(needs: new AiClarification("beehive", "Koja košnica?"));

        var candidates = Build(envelope, Context());

        Assert.Equal(["Košnica 1", "Košnica 2"], candidates.Select(c => c.Label));
    }

    [Fact]
    public void A_needs_question_with_no_actions_and_too_many_apiaries_offers_nothing()
    {
        var many = Enumerable.Range(1, 9).Select(i => new ApiaryRef(i, $"Pčelinjak {i}")).ToArray();
        var envelope = Envelope(needs: new AiClarification("apiary", "Na kojem pčelinjaku?"));

        Assert.Empty(Build(envelope, Context(many, [])));
    }

    // ── The ceiling wins over everything ──────────────────────────────────────

    [Fact]
    public void An_exceeded_ceiling_never_offers_buttons_even_if_an_action_would_otherwise_qualify()
    {
        // Built directly rather than through Resolve — this asserts the short-circuit itself, not a
        // scenario the resolver happens to produce today.
        var ambiguousAction = new AiResolvedAction(
            AiActionKind.CreateInspection, new AiActionFields(), [],
            AiResolutionIssue.ApiaryNotFound, ApiaryCandidates: [Zlatna, Livada]);
        var resolution = new AiResolution([ambiguousAction], TotalTargets: 999, ExceededCeiling: true);
        var envelope = Envelope();

        Assert.Empty(AssistantClarificationBuilder.Build(envelope, resolution, Context()));
    }

    // ── Phase C: existing-record ambiguity ───────────────────────────────────

    [Fact]
    public void Two_todos_sharing_a_title_offer_them_as_buttons_labelled_by_where_they_live()
    {
        var onZlatnaHive2 = new TodoRef(501, "Dodaj postolje", null, TodoPriority.Medium, null, false, null, 11);
        var onLivada = new TodoRef(502, "Dodaj postolje", null, TodoPriority.Medium, null, false, 2, null);
        var envelope = Envelope(actions: [CompleteTodoAction("Dodaj postolje")]);

        var candidates = Build(envelope, Context(todos: [onZlatnaHive2, onLivada]));

        Assert.Equal(["Dodaj postolje — Košnica 2", "Dodaj postolje — Livada"], candidates.Select(c => c.Label));
        // Tapping one must actually disambiguate on the next pass — the bare title alone would not.
        Assert.All(candidates, c => Assert.Contains("—", c.Text));
    }

    [Fact]
    public void More_than_eight_matching_todos_falls_back_to_retyping_a_more_specific_command()
    {
        var many = Enumerable.Range(1, 9)
            .Select(i => new TodoRef(500 + i, "Dodaj postolje", null, TodoPriority.Medium, null, false, i, null))
            .ToArray();
        var envelope = Envelope(actions: [CompleteTodoAction("Dodaj postolje")]);

        Assert.Empty(Build(envelope, Context(apiaries: many.Select(t => new ApiaryRef(t.ApiaryId!.Value, $"Apiary {t.ApiaryId}")).ToArray(),
            todos: many)));
    }

    [Fact]
    public void Two_inspections_on_the_same_date_offer_them_as_buttons()
    {
        var first = new InspectionRef(601, new DateTime(2026, 8, 5), HoneyLevel.Medium, null, null, 11);
        var second = first with { Id = 602 };
        var envelope = Envelope(actions: [DeleteInspectionAction("Zlatna dolina", Numbers("2"), new DateOnly(2026, 8, 5))]);

        var candidates = Build(envelope, Context(inspections: [first, second]));

        Assert.Equal(["Pregled 05.08.2026", "Pregled 05.08.2026"], candidates.Select(c => c.Label));
        Assert.Equal(["2026-08-05", "2026-08-05"], candidates.Select(c => c.Text));
    }

    [Fact]
    public void A_resolved_existing_record_action_offers_no_candidates()
    {
        var todo = new TodoRef(501, "Dodaj postolje", null, TodoPriority.Medium, null, false, null, 11);
        var envelope = Envelope(actions: [CompleteTodoAction("Dodaj postolje")]);

        Assert.Empty(Build(envelope, Context(todos: [todo])));
    }
}
