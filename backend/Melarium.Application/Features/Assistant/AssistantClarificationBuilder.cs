namespace Melarium.Application.Features.Assistant;

/// <summary>
/// Turns a resolution's unresolved bits into tappable candidates (SPEC-17 Phase B, D7). Pure, no I/O —
/// it only reads what <see cref="AiTargetResolver"/> already computed, so it can never see more than the
/// caller's accessible apiaries and hives (the same invariant §1 states for resolution itself).
///
/// Deliberately conservative: it returns candidates only where tapping one is unambiguous and the list
/// is short enough to render as buttons. Anything wider falls through to the card's own dropdown
/// (Phase A) — the question is the faster path, never the only one.
/// </summary>
public static class AssistantClarificationBuilder
{
    private const int MaxCandidates = 8;

    public static IReadOnlyList<AssistantCandidate> Build(
        AiEnvelope envelope, AiResolution resolution, AiResolutionContext context)
    {
        // The ceiling message already explains itself in words ("suzite naredbu…") — a button here
        // would invite tapping into another oversized batch instead of narrowing the command.
        if (resolution.ExceededCeiling) return [];

        foreach (var action in resolution.Actions)
        {
            var fromAction = FromAction(action, context);
            if (fromAction.Count > 0) return fromAction;
        }

        return FromNeeds(envelope, context);
    }

    private static IReadOnlyList<AssistantCandidate> FromAction(AiResolvedAction action, AiResolutionContext context)
    {
        // An unresolved/ambiguous apiary is the most common case (SPEC-17 example: "Nepostojeći
        // pčelinjak") and the most valuable one to ask about first — it narrows everything downstream.
        if (action.ApiaryCandidateList.Count is > 0 and <= MaxCandidates)
            return action.ApiaryCandidateList.Select(ToApiaryCandidate).ToList();

        if (action.HiveCandidateList.Count > 0)
        {
            var apiaryIds = action.HiveCandidateList.Select(h => h.ApiaryId).Distinct().ToList();

            // The same hive NUMBER exists on several apiaries (HiveAmbiguous) — the number is already
            // known, so the useful question is "which apiary", not "which hive".
            if (apiaryIds.Count > 1)
            {
                if (apiaryIds.Count > MaxCandidates) return [];
                return apiaryIds
                    .Select(id => context.Apiaries.FirstOrDefault(a => a.Id == id))
                    .Where(a => a is not null)
                    .Select(a => ToApiaryCandidate(a!))
                    .ToList();
            }

            // One apiary is already settled and it has a short list of hives to choose from
            // (MissingHive) — offer the hives themselves.
            if (action.HiveCandidateList.Count <= MaxCandidates)
                return action.HiveCandidateList.Select(ToHiveCandidate).ToList();
        }

        // Phase C: several open todos share the same (or a matching) title.
        if (action.TodoCandidateList.Count is > 0 and <= MaxCandidates)
            return action.TodoCandidateList.Select(t => ToTodoCandidate(t, context)).ToList();

        // Phase C: a hive has more than one inspection on the named date.
        if (action.InspectionCandidateList.Count is > 0 and <= MaxCandidates)
            return action.InspectionCandidateList.Select(ToInspectionCandidate).ToList();

        return [];
    }

    /// <summary>
    /// The model itself asked a question (empty <c>actions</c>) with nothing more specific to go on.
    /// The full accessible set stands in for "candidates the resolver found" — still capped, so a
    /// large organization degrades to the question text plus the card's dropdown, not a button wall.
    /// </summary>
    private static IReadOnlyList<AssistantCandidate> FromNeeds(AiEnvelope envelope, AiResolutionContext context)
    {
        if (envelope.Needs is not { } needs) return [];

        var pool = needs.What.Trim().Equals("beehive", StringComparison.OrdinalIgnoreCase)
            ? context.Hives.Select(ToHiveCandidate).ToList()
            : context.Apiaries.Select(ToApiaryCandidate).ToList();

        return pool.Count is > 0 and <= MaxCandidates ? pool : [];
    }

    private static AssistantCandidate ToApiaryCandidate(ApiaryRef apiary) => new(apiary.Name, apiary.Name);

    /// <summary>
    /// <see cref="AssistantCandidate.Text"/> is what gets sent as the next turn's message — the label
    /// number ("2") is what a beekeeper actually says, so it is preferred over the display name when set.
    /// </summary>
    private static AssistantCandidate ToHiveCandidate(HiveRef hive) =>
        new(hive.Name, hive.LabelNumber ?? hive.Name);

    /// <summary>
    /// Two todos sharing a title only differ by where they live, so both the button and the text sent
    /// back name the hive/apiary — the title alone would just match both again on the next pass.
    /// </summary>
    private static AssistantCandidate ToTodoCandidate(TodoRef todo, AiResolutionContext context)
    {
        var hive = todo.BeehiveId is int bId ? context.Hives.FirstOrDefault(h => h.Id == bId) : null;
        var apiaryName = (todo.ApiaryId ?? hive?.ApiaryId) is int apiaryId
            ? context.Apiaries.FirstOrDefault(a => a.Id == apiaryId)?.Name
            : null;
        var where = hive?.Name ?? apiaryName;
        var label = where is null ? todo.Title : $"{todo.Title} — {where}";
        return new AssistantCandidate(label, label);
    }

    private static AssistantCandidate ToInspectionCandidate(InspectionRef inspection)
    {
        var label = $"Pregled {inspection.Date:dd.MM.yyyy}";
        return new AssistantCandidate(label, inspection.Date.ToString("yyyy-MM-dd"));
    }
}
