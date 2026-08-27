using Melarium.Application.Features.Assistant;
using Melarium.Domain.Enums;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The parser is the app's only defence against whatever the model actually returns (SPEC-17 §3), so
/// every case here is a shape the model has a real chance of producing. It must never throw: a bad
/// action is dropped, a bad envelope is null, and the user gets a message instead of a 500.
/// </summary>
public class AiEnvelopeParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ovo nije JSON")]
    [InlineData("[1,2,3]")]          // valid JSON, wrong shape
    [InlineData("\"samo string\"")]
    public void Unusable_payloads_return_null(string? json) =>
        Assert.Null(AiEnvelopeParser.Parse(json));

    [Fact]
    public void Parses_the_worked_example_from_the_spec()
    {
        var envelope = AiEnvelopeParser.Parse(
            """
            {
              "reply": "Unosim pregled i zadatak.",
              "needs": null,
              "actions": [
                { "kind": "create_inspection", "apiary": "Zlatna dolina", "hives": ["2"],
                  "fields": { "date": "2026-08-08", "honeyLevel": 2, "broodStatus": "5 ramova legla", "notes": null } },
                { "kind": "create_todo", "apiary": "Zlatna dolina", "hives": ["2"],
                  "fields": { "title": "Pregled košnice 2", "priority": 2, "dueDate": "2026-08-18" } }
              ]
            }
            """);

        Assert.NotNull(envelope);
        Assert.Equal("Unosim pregled i zadatak.", envelope!.Reply);
        Assert.Null(envelope.Needs);
        Assert.Equal(2, envelope.Actions.Count);

        var inspection = envelope.Actions[0];
        Assert.Equal(AiActionKind.CreateInspection, inspection.Kind);
        Assert.Equal("Zlatna dolina", inspection.Apiary);
        Assert.Equal(["2"], inspection.Hives.Numbers);
        Assert.Equal(new DateOnly(2026, 8, 8), inspection.Fields.Date);
        Assert.Equal(HoneyLevel.Medium, inspection.Fields.HoneyLevel);
        Assert.Equal("5 ramova legla", inspection.Fields.BroodStatus);
        Assert.Null(inspection.Fields.Notes);

        var todo = envelope.Actions[1];
        Assert.Equal(AiActionKind.CreateTodo, todo.Kind);
        Assert.Equal("Pregled košnice 2", todo.Fields.Title);
        Assert.Equal(TodoPriority.Medium, todo.Fields.Priority);
        Assert.Equal(new DateOnly(2026, 8, 18), todo.Fields.DueDate);
    }

    [Fact]
    public void Unknown_action_kind_is_dropped_but_the_rest_of_the_turn_survives()
    {
        var envelope = AiEnvelopeParser.Parse(
            """
            {
              "reply": "ok",
              "actions": [
                { "kind": "delete_everything", "hives": ["1"], "fields": {} },
                { "kind": "create_todo", "apiary": "X", "hives": null, "fields": { "title": "Popravi krov" } }
              ]
            }
            """);

        var action = Assert.Single(envelope!.Actions);
        Assert.Equal(AiActionKind.CreateTodo, action.Kind);
        Assert.Equal("Popravi krov", action.Fields.Title);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    [InlineData(99)]
    public void Out_of_range_enum_values_become_null_rather_than_a_meaningless_stored_value(int honeyLevel)
    {
        var envelope = AiEnvelopeParser.Parse(
            $$"""
            { "actions": [ { "kind": "create_inspection", "hives": ["1"], "fields": { "honeyLevel": {{honeyLevel}} } } ] }
            """);

        Assert.Null(envelope!.Actions[0].Fields.HoneyLevel);
    }

    [Theory]
    [InlineData("\"sutra\"")]
    [InlineData("\"08.08.2026\"")]
    [InlineData("\"2026-13-45\"")]
    [InlineData("\"2026-08-08T10:00:00Z\"")]
    [InlineData("null")]
    public void Dates_that_are_not_strict_yyyy_MM_dd_become_null(string raw)
    {
        var envelope = AiEnvelopeParser.Parse(
            $$"""
            { "actions": [ { "kind": "create_inspection", "hives": ["1"], "fields": { "date": {{raw}} } } ] }
            """);

        Assert.Null(envelope!.Actions[0].Fields.Date);
    }

    [Theory]
    [InlineData("\"all\"")]
    [InlineData("\"ALL\"")]
    [InlineData("\"sve\"")]
    [InlineData("[\"all\"]")]
    public void All_is_recognised_however_the_model_spells_it(string hives)
    {
        var envelope = AiEnvelopeParser.Parse(
            $$"""
            { "actions": [ { "kind": "create_inspection", "apiary": "X", "hives": {{hives}}, "fields": {} } ] }
            """);

        Assert.True(envelope!.Actions[0].Hives.All);
        Assert.Empty(envelope.Actions[0].Hives.Numbers);
    }

    [Fact]
    public void Bare_numbers_are_accepted_because_the_model_emits_them_despite_the_prompt()
    {
        var envelope = AiEnvelopeParser.Parse(
            """
            { "actions": [ { "kind": "create_inspection", "apiary": "X", "hives": [1, 2, 2, 3], "fields": {} } ] }
            """);

        Assert.Equal(["1", "2", "3"], envelope!.Actions[0].Hives.Numbers);
    }

    [Fact]
    public void Missing_hives_and_fields_do_not_throw()
    {
        var envelope = AiEnvelopeParser.Parse("""{ "actions": [ { "kind": "create_todo" } ] }""");

        var action = Assert.Single(envelope!.Actions);
        Assert.True(action.Hives.IsEmpty);
        Assert.Null(action.Fields.Title);
        Assert.Null(action.Apiary);
    }

    [Fact]
    public void A_json_code_fence_is_stripped_rather_than_losing_the_turn()
    {
        var envelope = AiEnvelopeParser.Parse(
            "```json\n{ \"reply\": \"ok\", \"actions\": [] }\n```");

        Assert.Equal("ok", envelope!.Reply);
    }

    [Fact]
    public void Clarification_is_read_when_present_and_ignored_when_it_has_no_question()
    {
        var withQuestion = AiEnvelopeParser.Parse(
            """{ "reply": "", "needs": { "what": "Apiary", "question": "Na kojem pčelinjaku?" }, "actions": [] }""");
        Assert.Equal("apiary", withQuestion!.Needs!.What);
        Assert.Equal("Na kojem pčelinjaku?", withQuestion.Needs.Question);

        var empty = AiEnvelopeParser.Parse("""{ "needs": { "what": "apiary" }, "actions": [] }""");
        Assert.Null(empty!.Needs);
    }

    [Fact]
    public void Blank_strings_become_null_so_they_do_not_reach_the_database_as_empty_values()
    {
        var envelope = AiEnvelopeParser.Parse(
            """
            { "actions": [ { "kind": "create_todo", "apiary": "   ",
              "fields": { "title": "  Naslov  ", "notes": "   " } } ] }
            """);

        var action = envelope!.Actions[0];
        Assert.Null(action.Apiary);
        Assert.Equal("Naslov", action.Fields.Title);
        Assert.Null(action.Fields.Notes);
    }

    // ── Phase C: existing-record kinds ──────────────────────────────────────

    [Theory]
    [InlineData("update_todo", AiActionKind.UpdateTodo)]
    [InlineData("complete_todo", AiActionKind.CompleteTodo)]
    [InlineData("update_inspection", AiActionKind.UpdateInspection)]
    [InlineData("delete_todo", AiActionKind.DeleteTodo)]
    [InlineData("delete_inspection", AiActionKind.DeleteInspection)]
    public void Phase_C_kinds_are_recognised(string raw, AiActionKind expected)
    {
        var envelope = AiEnvelopeParser.Parse(
            $$"""{ "actions": [ { "kind": "{{raw}}", "targetTitle": "Dodaj postolje", "fields": {} } ] }""");

        Assert.Equal(expected, envelope!.Actions[0].Kind);
    }

    [Fact]
    public void TargetTitle_is_read_and_trimmed()
    {
        var envelope = AiEnvelopeParser.Parse(
            """{ "actions": [ { "kind": "complete_todo", "targetTitle": "  Dodaj postolje  ", "fields": {} } ] }""");

        Assert.Equal("Dodaj postolje", envelope!.Actions[0].TargetTitle);
    }

    [Fact]
    public void A_blank_TargetTitle_becomes_null_like_every_other_string_field()
    {
        var envelope = AiEnvelopeParser.Parse(
            """{ "actions": [ { "kind": "delete_todo", "targetTitle": "   ", "fields": {} } ] }""");

        Assert.Null(envelope!.Actions[0].TargetTitle);
    }

    [Fact]
    public void Create_actions_have_no_TargetTitle_when_the_model_does_not_send_one()
    {
        var envelope = AiEnvelopeParser.Parse(
            """{ "actions": [ { "kind": "create_todo", "fields": { "title": "Novi zadatak" } } ] }""");

        Assert.Null(envelope!.Actions[0].TargetTitle);
    }

    // ── Colony merge (SPEC-19 §8) ────────────────────────────────────────────

    [Fact]
    public void Parses_a_merge_action()
    {
        var envelope = AiEnvelopeParser.Parse(
            """
            { "actions": [ { "kind": "merge_beehive", "hives": ["5"],
              "fields": { "targetHive": "3", "queenOutcome": 1, "mergeReason": 1 } } ] }
            """);

        var action = envelope!.Actions[0];
        Assert.Equal(AiActionKind.MergeBeehive, action.Kind);
        Assert.Equal(["5"], action.Hives.Numbers);
        Assert.Equal("3", action.Fields.TargetHive);
        Assert.Equal(MergeQueenOutcome.KeptTarget, action.Fields.QueenOutcome);
        Assert.Equal(MergeReason.Queenless, action.Fields.MergeReason);
    }

    [Fact]
    public void A_bare_number_targetHive_is_accepted()
    {
        var envelope = AiEnvelopeParser.Parse(
            """{ "actions": [ { "kind": "merge_beehive", "hives": ["5"], "fields": { "targetHive": 3 } } ] }""");

        Assert.Equal("3", envelope!.Actions[0].Fields.TargetHive);
    }

    [Fact]
    public void An_omitted_queenOutcome_stays_null_rather_than_defaulting()
    {
        var envelope = AiEnvelopeParser.Parse(
            """{ "actions": [ { "kind": "merge_beehive", "hives": ["5"], "fields": { "targetHive": "3" } } ] }""");

        // The one field the resolver turns into a question instead of guessing (SPEC-19 §8).
        Assert.Null(envelope!.Actions[0].Fields.QueenOutcome);
    }

    [Fact]
    public void An_out_of_range_queenOutcome_is_dropped_not_coerced()
    {
        var envelope = AiEnvelopeParser.Parse(
            """{ "actions": [ { "kind": "merge_beehive", "hives": ["5"], "fields": { "targetHive": "3", "queenOutcome": 9 } } ] }""");

        Assert.Null(envelope!.Actions[0].Fields.QueenOutcome);
    }
}
