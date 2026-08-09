using System.Globalization;
using System.Text;
using Melarium.Application.Features.Beehives;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Assistant;

/// <summary>
/// Maps the names and numbers the model echoed onto real apiaries and hives (SPEC-17 §4). Pure, no
/// I/O, and it only ever searches the sets handed to it in <see cref="AiResolutionContext"/> — which
/// come from <see cref="Common.Security.IAccessGuard"/>. A hive the caller cannot reach is therefore
/// not "forbidden", it is invisible, and no future action kind can accidentally skip that check.
/// </summary>
public static class AiTargetResolver
{
    public static AiResolution Resolve(AiEnvelope envelope, AiResolutionContext context)
    {
        var resolved = envelope.Actions.Select(a => ResolveAction(a, context)).ToList();
        var total = resolved.Sum(a => a.Targets.Count);

        if (total <= context.MaxTargets)
            return new AiResolution(resolved, total, ExceededCeiling: false);

        // Over the ceiling nothing is offered for confirmation — the card reports the count and asks
        // the user to narrow the command, rather than silently creating the first N records.
        var stripped = resolved
            .Select(a => a with { Targets = [] })
            .ToList();
        return new AiResolution(stripped, total, ExceededCeiling: true);
    }

    // ── One action ────────────────────────────────────────────────────────────

    private static AiResolvedAction ResolveAction(AiProposedAction action, AiResolutionContext ctx)
    {
        // Phase C: an update/complete/delete targets an EXISTING record, found by title (todo) or
        // date (inspection) — a different search entirely from "where does a new record go".
        if (action.Kind is AiActionKind.UpdateTodo or AiActionKind.CompleteTodo or AiActionKind.DeleteTodo)
            return ResolveExistingTodo(action, ctx);
        if (action.Kind is AiActionKind.UpdateInspection or AiActionKind.DeleteInspection)
            return ResolveExistingInspection(action, ctx);

        var (apiary, apiaryIssue, apiaryCandidates) = ResolveApiary(action.Apiary, ctx);

        if (apiaryIssue != AiResolutionIssue.None && action.Apiary is not null)
            return new AiResolvedAction(action.Kind, action.Fields, [], apiaryIssue, apiaryCandidates);

        // A todo needs a title before anything else is worth resolving.
        if (action.Kind == AiActionKind.CreateTodo && string.IsNullOrWhiteSpace(action.Fields.Title))
            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.MissingTitle, apiaryCandidates);

        return action.Hives switch
        {
            { All: true }                => ResolveAllHives(action, apiary, ctx, apiaryCandidates),
            { Numbers.Count: > 0 }       => ResolveNumberedHives(action, apiary, ctx),
            _                            => ResolveWithoutHive(action, apiary, ctx, apiaryCandidates),
        };
    }

    // ── "sve košnice" ─────────────────────────────────────────────────────────

    private static AiResolvedAction ResolveAllHives(
        AiProposedAction action, ApiaryRef? apiary, AiResolutionContext ctx, IReadOnlyList<ApiaryRef> apiaryCandidates)
    {
        // "sve košnice" is a wildcard over one apiary, never over the whole organization — on a
        // multi-apiary account an unnamed apiary is an ambiguity, not permission to touch everything.
        if (apiary is null)
            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.ApiaryAmbiguous, ctx.Apiaries);

        var hives = ctx.Hives.Where(h => h.ApiaryId == apiary.Id).OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (hives.Count == 0)
            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.NoHivesInApiary, apiaryCandidates);

        var targets = hives.Select(h => new AiResolvedTarget(apiary.Id, apiary.Name, h.Id, h.Name)).ToList();
        return new AiResolvedAction(action.Kind, action.Fields, targets);
    }

    // ── "košnica 2", "košnice 1, 2 i 3" ───────────────────────────────────────

    private static AiResolvedAction ResolveNumberedHives(
        AiProposedAction action, ApiaryRef? apiary, AiResolutionContext ctx)
    {
        // With no apiary named, search everything reachable — the same fallback the hive scanner uses.
        var pool = apiary is null ? ctx.Hives : ctx.Hives.Where(h => h.ApiaryId == apiary.Id).ToList();

        var targets = new List<AiResolvedTarget>();
        var unresolved = new List<string>();
        var candidates = new List<HiveRef>();
        var issue = AiResolutionIssue.None;

        foreach (var number in action.Hives.Numbers)
        {
            var canonical = HiveNumberMatcher.Normalize(number);
            if (canonical is null)
            {
                unresolved.Add(number);
                if (issue == AiResolutionIssue.None) issue = AiResolutionIssue.HiveNotFound;
                continue;
            }

            var matches = pool.Where(h => HiveNumberMatcher.Matches(h.LabelNumber, h.Name, canonical)).ToList();

            switch (matches.Count)
            {
                case 1:
                    targets.Add(ToTarget(matches[0], ctx));
                    break;

                case 0:
                    unresolved.Add(number);
                    if (issue == AiResolutionIssue.None) issue = AiResolutionIssue.HiveNotFound;
                    break;

                default:
                    // The same number on two apiaries. Never guess — carry the candidates out so the
                    // user picks, either from the dropdown or from the follow-up question.
                    unresolved.Add(number);
                    candidates.AddRange(matches);
                    issue = AiResolutionIssue.HiveAmbiguous;
                    break;
            }
        }

        return new AiResolvedAction(
            action.Kind, action.Fields, targets, issue,
            ApiaryCandidates: null,
            HiveCandidates: candidates.Count > 0 ? candidates : null,
            UnresolvedHiveNumbers: unresolved.Count > 0 ? unresolved : null);
    }

    // ── No hive named ─────────────────────────────────────────────────────────

    private static AiResolvedAction ResolveWithoutHive(
        AiProposedAction action, ApiaryRef? apiary, AiResolutionContext ctx, IReadOnlyList<ApiaryRef> apiaryCandidates)
    {
        // The page the user is standing on fills the gap — "unesi pregled" on a hive page means this hive.
        if (ctx.PageBeehiveId is int pageHiveId)
        {
            var hive = ctx.Hives.FirstOrDefault(h => h.Id == pageHiveId);
            if (hive is not null)
                return new AiResolvedAction(action.Kind, action.Fields, [ToTarget(hive, ctx)]);
        }

        // A todo may legitimately belong to the apiary rather than to a hive (SPEC-17 example 2).
        if (action.Kind == AiActionKind.CreateTodo)
        {
            if (apiary is not null)
                return new AiResolvedAction(action.Kind, action.Fields,
                    [new AiResolvedTarget(apiary.Id, apiary.Name, null, null)]);

            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.ApiaryAmbiguous, ctx.Apiaries);
        }

        // An inspection cannot exist without a hive.
        var hiveChoices = apiary is null ? ctx.Hives : ctx.Hives.Where(h => h.ApiaryId == apiary.Id).ToList();
        return new AiResolvedAction(
            action.Kind, action.Fields, [], AiResolutionIssue.MissingHive,
            apiaryCandidates, hiveChoices);
    }

    // ── Phase C: an existing todo, by title ─────────────────────────────────────

    private static AiResolvedAction ResolveExistingTodo(AiProposedAction action, AiResolutionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(action.TargetTitle))
            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.MissingTitle);

        var (apiary, apiaryIssue, apiaryCandidates) = ResolveApiary(action.Apiary, ctx);
        if (apiaryIssue != AiResolutionIssue.None && action.Apiary is not null)
            return new AiResolvedAction(action.Kind, action.Fields, [], apiaryIssue, apiaryCandidates);

        var pool = ctx.TodoPool.AsEnumerable();

        // A named hive narrows the search the same way it would for a new record; a named apiary
        // (with no specific hive) narrows to that apiary's own todos plus its hives' todos.
        if (action.Hives.Numbers.Count == 1)
        {
            var canonical = HiveNumberMatcher.Normalize(action.Hives.Numbers[0]);
            var hivePool = apiary is null ? ctx.Hives : ctx.Hives.Where(h => h.ApiaryId == apiary.Id);
            var hive = canonical is null ? null : hivePool.FirstOrDefault(h => HiveNumberMatcher.Matches(h.LabelNumber, h.Name, canonical));
            if (hive is not null)
                pool = pool.Where(t => t.BeehiveId == hive.Id);
        }
        else if (apiary is not null)
        {
            var hiveIds = ctx.Hives.Where(h => h.ApiaryId == apiary.Id).Select(h => h.Id).ToHashSet();
            pool = pool.Where(t => t.ApiaryId == apiary.Id || (t.BeehiveId is int bId && hiveIds.Contains(bId)));
        }

        var needle = Normalize(action.TargetTitle);
        var exact = pool.Where(t => Normalize(t.Title) == needle).ToList();
        var matches = exact.Count > 0 ? exact : pool.Where(t => Normalize(t.Title).Contains(needle, StringComparison.Ordinal)).ToList();

        if (matches.Count == 0)
            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.TodoNotFound);
        if (matches.Count > 1)
            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.TodoAmbiguous, TodoCandidates: matches);

        return new AiResolvedAction(action.Kind, action.Fields, [ToTarget(matches[0], ctx)]);
    }

    // ── Phase C: an existing inspection, by hive + date ─────────────────────────

    private static AiResolvedAction ResolveExistingInspection(AiProposedAction action, AiResolutionContext ctx)
    {
        var (apiary, apiaryIssue, apiaryCandidates) = ResolveApiary(action.Apiary, ctx);
        if (apiaryIssue != AiResolutionIssue.None && action.Apiary is not null)
            return new AiResolvedAction(action.Kind, action.Fields, [], apiaryIssue, apiaryCandidates);

        // An inspection belongs to exactly one hive — same requirement as creating one.
        if (action.Hives.Numbers.Count != 1)
        {
            var hiveChoices = apiary is null ? ctx.Hives : ctx.Hives.Where(h => h.ApiaryId == apiary.Id).ToList();
            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.MissingHive, apiaryCandidates, hiveChoices);
        }

        var canonical = HiveNumberMatcher.Normalize(action.Hives.Numbers[0]);
        var hivePool = apiary is null ? ctx.Hives : ctx.Hives.Where(h => h.ApiaryId == apiary.Id).ToList();
        var hiveMatches = canonical is null ? [] : hivePool.Where(h => HiveNumberMatcher.Matches(h.LabelNumber, h.Name, canonical)).ToList();

        if (hiveMatches.Count == 0)
            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.HiveNotFound);
        if (hiveMatches.Count > 1)
            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.HiveAmbiguous, HiveCandidates: hiveMatches);

        var hive = hiveMatches[0];
        var inspectionPool = ctx.InspectionPool.Where(i => i.BeehiveId == hive.Id).ToList();

        // No date named — "zadnji pregled" (the most recent one) is the natural reading, so that is
        // the default rather than an ambiguity across every inspection the hive has ever had.
        var matches = action.Fields.Date is { } date
            ? inspectionPool.Where(i => DateOnly.FromDateTime(i.Date) == date).ToList()
            : inspectionPool.OrderByDescending(i => i.Date).Take(1).ToList();

        if (matches.Count == 0)
            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.InspectionNotFound);
        if (matches.Count > 1)
            return new AiResolvedAction(action.Kind, action.Fields, [], AiResolutionIssue.InspectionAmbiguous, InspectionCandidates: matches);

        return new AiResolvedAction(action.Kind, action.Fields, [ToTarget(matches[0], hive, ctx)]);
    }

    // ── Apiary matching ───────────────────────────────────────────────────────

    private static (ApiaryRef? Apiary, AiResolutionIssue Issue, IReadOnlyList<ApiaryRef> Candidates) ResolveApiary(
        string? spoken, AiResolutionContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(spoken))
        {
            var needle = Normalize(spoken);

            var exact = ctx.Apiaries.Where(a => Normalize(a.Name) == needle).ToList();
            if (exact.Count == 1) return (exact[0], AiResolutionIssue.None, []);
            if (exact.Count > 1) return (null, AiResolutionIssue.ApiaryAmbiguous, exact);

            // "Zlatna" for "Zlatna dolina", and "pčelinjak Zlatna dolina" for "Zlatna dolina".
            var partial = ctx.Apiaries
                .Where(a => Normalize(a.Name).Contains(needle, StringComparison.Ordinal)
                         || needle.Contains(Normalize(a.Name), StringComparison.Ordinal))
                .ToList();
            if (partial.Count == 1) return (partial[0], AiResolutionIssue.None, []);
            if (partial.Count > 1) return (null, AiResolutionIssue.ApiaryAmbiguous, partial);

            return (null, AiResolutionIssue.ApiaryNotFound, ctx.Apiaries);
        }

        if (ctx.PageApiaryId is int pageApiaryId)
        {
            var fromPage = ctx.Apiaries.FirstOrDefault(a => a.Id == pageApiaryId);
            if (fromPage is not null) return (fromPage, AiResolutionIssue.None, []);
        }

        if (ctx.Apiaries.Count == 1)
            return (ctx.Apiaries[0], AiResolutionIssue.None, []);

        // Unscoped. Hive numbers may still identify the target on their own.
        return (null, AiResolutionIssue.None, ctx.Apiaries);
    }

    private static AiResolvedTarget ToTarget(HiveRef hive, AiResolutionContext ctx)
    {
        var apiaryName = ctx.Apiaries.FirstOrDefault(a => a.Id == hive.ApiaryId)?.Name ?? "—";
        return new AiResolvedTarget(hive.ApiaryId, apiaryName, hive.Id, hive.Name);
    }

    /// <summary>An existing todo as the update/complete/delete target — carries its own "before" state.</summary>
    private static AiResolvedTarget ToTarget(TodoRef todo, AiResolutionContext ctx)
    {
        var hive = todo.BeehiveId is int bId ? ctx.Hives.FirstOrDefault(h => h.Id == bId) : null;
        var apiaryId = todo.ApiaryId ?? hive?.ApiaryId ?? 0;
        var apiaryName = ctx.Apiaries.FirstOrDefault(a => a.Id == apiaryId)?.Name ?? "—";

        return new AiResolvedTarget(
            apiaryId, apiaryName, todo.BeehiveId, hive?.Name,
            ExistingEntityId: todo.Id, ExistingEntityType: nameof(Domain.Entities.Todo),
            ExistingSummary: todo.Title,
            PreviousFields: new AiActionFields(
                Title: todo.Title, Notes: todo.Notes, Priority: todo.Priority,
                DueDate: todo.DueDate is { } due ? DateOnly.FromDateTime(due) : null));
    }

    /// <summary>An existing inspection as the update/delete target — carries its own "before" state.</summary>
    private static AiResolvedTarget ToTarget(InspectionRef inspection, HiveRef hive, AiResolutionContext ctx)
    {
        var apiaryName = ctx.Apiaries.FirstOrDefault(a => a.Id == hive.ApiaryId)?.Name ?? "—";
        var dateLabel = inspection.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return new AiResolvedTarget(
            hive.ApiaryId, apiaryName, hive.Id, hive.Name,
            ExistingEntityId: inspection.Id, ExistingEntityType: nameof(Domain.Entities.Inspection),
            ExistingSummary: $"Pregled {dateLabel} — {hive.Name}",
            PreviousFields: new AiActionFields(
                Date: DateOnly.FromDateTime(inspection.Date), HoneyLevel: inspection.HoneyLevel,
                BroodStatus: inspection.BroodStatus, Notes: inspection.Notes));
    }

    /// <summary>
    /// Comparison form for apiary names: lower-cased, diacritics folded, whitespace collapsed — so
    /// "Zlatna Dolina", "zlatna  dolina" and "Zlatna dolina" are one name. Speech-to-text drops
    /// diacritics constantly, which is why folding is not optional here.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Đ/đ is its own letter, not a decomposable one, so FormD leaves it untouched and it needs an
        // explicit mapping. It maps to "dj", the standard BCS transliteration and what both Whisper
        // and a phone keyboard without our letters produce — "Đurđevak" and "Djurdjevak" must be one
        // name. Mapping it to a bare "d" instead would make exactly that common pair fail to match.
        var prepared = raw.Replace("đ", "dj", StringComparison.Ordinal)
                          .Replace("Đ", "Dj", StringComparison.Ordinal);

        var decomposed = prepared.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsWhiteSpace(ch) ? ' ' : char.ToLowerInvariant(ch));
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
