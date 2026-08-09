using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Assistant;

/// <summary>An apiary the caller can reach, as far as resolution is concerned.</summary>
public sealed record ApiaryRef(int Id, string Name);

/// <summary>A beehive the caller can reach.</summary>
public sealed record HiveRef(int Id, string Name, string? LabelNumber, int ApiaryId);

/// <summary>
/// An existing, accessible todo — the Phase C search pool for update/complete/delete. Deliberately
/// carries every field a card needs to show "before" values, so resolving a target never requires a
/// second fetch once <see cref="AiResolutionContext.Todos"/> has been populated.
/// </summary>
public sealed record TodoRef(
    int Id, string Title, string? Notes, TodoPriority Priority, DateTime? DueDate,
    bool IsCompleted, int? ApiaryId, int? BeehiveId);

/// <summary>An existing, accessible inspection — the Phase C search pool for update/delete.</summary>
public sealed record InspectionRef(
    int Id, DateTime Date, HoneyLevel HoneyLevel, string? BroodStatus, string? Notes, int BeehiveId);

/// <summary>
/// Everything <see cref="AiTargetResolver"/> is allowed to see. The apiary and hive lists come from
/// <see cref="Common.Security.IAccessGuard"/>, so they already exclude anything out of the caller's
/// scope — that is what makes an inaccessible hive unresolvable rather than merely forbidden.
/// <see cref="Todos"/>/<see cref="Inspections"/> are the same idea for Phase C's existing-record
/// actions: <c>AiAssistantService</c> populates them from the caller's own accessible records
/// (<c>ITodoService.GetAllOpenForCurrentUserAsync</c>, <c>IInspectionService.GetByBeehiveIdAsync</c>)
/// — and only when the envelope actually contains an action that needs them, so a plain create command
/// never pays for a query it does not use.
/// </summary>
/// <param name="PageApiaryId">Apiary of the page the user is on; fills gaps only, never overrides a spoken name.</param>
/// <param name="PageBeehiveId">Hive of the page the user is on; same rule.</param>
/// <param name="MaxTargets">Ceiling on the total number of records one command may create.</param>
public sealed record AiResolutionContext(
    IReadOnlyList<ApiaryRef> Apiaries,
    IReadOnlyList<HiveRef> Hives,
    int? PageApiaryId = null,
    int? PageBeehiveId = null,
    int MaxTargets = 50,
    IReadOnlyList<TodoRef>? Todos = null,
    IReadOnlyList<InspectionRef>? Inspections = null)
{
    public IReadOnlyList<TodoRef> TodoPool => Todos ?? [];
    public IReadOnlyList<InspectionRef> InspectionPool => Inspections ?? [];
}

/// <summary>Why an action could not be turned into a confirmable card.</summary>
public enum AiResolutionIssue
{
    None = 0,

    /// <summary>A name was spoken but matched no apiary the caller can reach.</summary>
    ApiaryNotFound,

    /// <summary>Several apiaries match, or none was named and there is more than one to choose from.</summary>
    ApiaryAmbiguous,

    /// <summary>A hive number matched nothing.</summary>
    HiveNotFound,

    /// <summary>The same number exists on more than one reachable apiary.</summary>
    HiveAmbiguous,

    /// <summary>"sve košnice" on an apiary that has none.</summary>
    NoHivesInApiary,

    /// <summary>An inspection with no hive to attach to.</summary>
    MissingHive,

    /// <summary>A todo with no title.</summary>
    MissingTitle,

    /// <summary>Phase C: no accessible open todo matches the named title.</summary>
    TodoNotFound,

    /// <summary>Phase C: more than one accessible open todo matches the named title.</summary>
    TodoAmbiguous,

    /// <summary>Phase C: the resolved hive has no inspection matching the given (or default: most recent) date.</summary>
    InspectionNotFound,

    /// <summary>Phase C: more than one inspection matches (same hive, same date).</summary>
    InspectionAmbiguous,
}

/// <summary>
/// One concrete record the action would touch. For a create action this is a new record's apiary/hive;
/// for a Phase C update/complete/delete action it is an EXISTING record, identified by
/// <see cref="ExistingEntityId"/> — fixed once resolved and never re-picked from a dropdown, unlike a
/// create action's target (SPEC-17 §5.3: the confirm step still re-validates it, just not by letting the
/// client choose a different id).
/// </summary>
public sealed record AiResolvedTarget(
    int ApiaryId, string ApiaryName, int? BeehiveId, string? BeehiveName,
    int? ExistingEntityId = null, string? ExistingEntityType = null, string? ExistingSummary = null,
    AiActionFields? PreviousFields = null)
{
    /// <summary>History/card label — the existing record's own summary when there is one, else "Apiary · Hive".</summary>
    public string Summary => ExistingSummary ?? (BeehiveName is null ? ApiaryName : $"{ApiaryName} · {BeehiveName}");
}

/// <summary>
/// A proposed action with its targets resolved. <see cref="Targets"/> and <see cref="Issue"/> are not
/// exclusive: a command naming three hives where one is unknown resolves two targets *and* reports
/// the miss, so the user still gets the two cards.
/// </summary>
public sealed record AiResolvedAction(
    AiActionKind Kind,
    AiActionFields Fields,
    IReadOnlyList<AiResolvedTarget> Targets,
    AiResolutionIssue Issue = AiResolutionIssue.None,
    IReadOnlyList<ApiaryRef>? ApiaryCandidates = null,
    IReadOnlyList<HiveRef>? HiveCandidates = null,
    IReadOnlyList<string>? UnresolvedHiveNumbers = null,
    IReadOnlyList<TodoRef>? TodoCandidates = null,
    IReadOnlyList<InspectionRef>? InspectionCandidates = null)
{
    public IReadOnlyList<ApiaryRef> ApiaryCandidateList => ApiaryCandidates ?? [];
    public IReadOnlyList<HiveRef> HiveCandidateList => HiveCandidates ?? [];
    public IReadOnlyList<string> UnresolvedNumbers => UnresolvedHiveNumbers ?? [];
    public IReadOnlyList<TodoRef> TodoCandidateList => TodoCandidates ?? [];
    public IReadOnlyList<InspectionRef> InspectionCandidateList => InspectionCandidates ?? [];
}

/// <summary>Resolution of a whole envelope, so the target ceiling can apply across all its actions.</summary>
public sealed record AiResolution(
    IReadOnlyList<AiResolvedAction> Actions,
    int TotalTargets,
    bool ExceededCeiling);

/// <summary>
/// One tappable follow-up option (SPEC-17 Phase B). <see cref="Label"/> is shown on the button;
/// <see cref="Text"/> is sent as the next turn's message when tapped — it is ordinary user text, so it
/// goes through the same interpretation pipeline as anything typed, never a shortcut around it.
/// </summary>
public sealed record AssistantCandidate(string Label, string Text);
