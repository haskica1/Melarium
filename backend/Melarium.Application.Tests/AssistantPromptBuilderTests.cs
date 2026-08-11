using Melarium.Application.Features.Assistant;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks the system prompt's wording (SPEC-18): the assistant now answers questions instead of
/// deflecting to the retired advisor, the safety guardrails carried over from it are present, and the
/// date injection SPEC-17 §3.1 depends on still works.
/// </summary>
public class AssistantPromptBuilderTests
{
    private static readonly ApiaryRef[] NoApiaries = [];

    [Fact]
    public void The_old_deflection_to_the_advisor_is_gone()
    {
        var prompt = AssistantPromptBuilder.BuildSystem(NoApiaries, new DateOnly(2026, 8, 9));

        Assert.DoesNotContain("Ne daješ savjete", prompt);
        Assert.DoesNotContain("uputi na AI savjetnika", prompt);
    }

    [Fact]
    public void The_prompt_instructs_the_model_to_answer_questions_fully()
    {
        var prompt = AssistantPromptBuilder.BuildSystem(NoApiaries, new DateOnly(2026, 8, 9));

        Assert.Contains("KAD ODGOVARAŠ NA PITANJE", prompt);
        Assert.Contains("daj PUN odgovor", prompt);
    }

    [Fact]
    public void The_advisor_safety_guardrails_are_carried_over()
    {
        var prompt = AssistantPromptBuilder.BuildSystem(NoApiaries, new DateOnly(2026, 8, 9));

        Assert.Contains("PODLIJEŽE OBAVEZNOJ PRIJAVI", prompt); // AFB/EFB mandatory reporting
        Assert.Contains("Ne preporučuj doziranje lijekova", prompt);
        Assert.Contains("nije vezano za pčelarstvo", prompt);
    }

    [Fact]
    public void A_supplied_hive_context_block_is_appended_to_the_prompt()
    {
        var prompt = AssistantPromptBuilder.BuildSystem(
            NoApiaries, new DateOnly(2026, 8, 9), contextBlock: "PODACI O KOŠNICI (test-marker):\n- Košnica: Test");

        Assert.Contains("PODACI O KOŠNICI (test-marker)", prompt);
    }

    [Fact]
    public void A_null_context_block_adds_nothing_and_does_not_throw()
    {
        var prompt = AssistantPromptBuilder.BuildSystem(NoApiaries, new DateOnly(2026, 8, 9), contextBlock: null);

        Assert.DoesNotContain("PODACI O KOŠNICI", prompt);
    }

    [Fact]
    public void Todays_date_is_injected_exactly_as_given_never_recomputed_from_UtcNow()
    {
        // SPEC-17 §3.1 / ADR-033: "danas" must come from AppTimeZone.Today(tz), never DateTime.UtcNow —
        // this only locks that whatever DateOnly the caller passes in reaches the prompt unchanged.
        var prompt = AssistantPromptBuilder.BuildSystem(NoApiaries, new DateOnly(2026, 8, 9));

        Assert.Contains("Današnji datum: 2026-08-09", prompt);
    }

    [Fact]
    public void Action_kinds_and_json_shape_rules_are_still_present()
    {
        // A coarse regression guard — SPEC-18 only touches the intro and rule 6, not the action
        // catalogue Phases A/B/C built.
        var prompt = AssistantPromptBuilder.BuildSystem(NoApiaries, new DateOnly(2026, 8, 9));

        Assert.Contains("\"create_inspection\"", prompt);
        Assert.Contains("\"update_todo\"", prompt);
        Assert.Contains("\"delete_inspection\"", prompt);
    }
}
