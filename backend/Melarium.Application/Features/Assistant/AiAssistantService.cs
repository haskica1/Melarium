using System.Text.Json;
using Melarium.Application.Common;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Assistant.DTOs;
using Melarium.Application.Features.Beehives;
using Melarium.Application.Features.Inspections;
using Melarium.Application.Features.Todos;
using Melarium.Application.Features.Weather;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using ValidationException = Melarium.Application.Common.Exceptions.ValidationException;

namespace Melarium.Application.Features.Assistant;

/// <summary>
/// AI assistant (SPEC-17). Turns a spoken or typed command into confirmable proposals, then executes
/// the confirmed ones through the existing application services. Nothing here writes an inspection or
/// a todo directly — see <see cref="AiActionExecutor"/> and SPEC-17 §5.1.
/// </summary>
public class AiAssistantService : IAiAssistantService
{
    private const int MaxTurns = 40;
    private const int HistoryWindow = 8;

    private readonly IUnitOfWork _uow;
    private readonly IAccessGuard _access;
    private readonly ICurrentUser _currentUser;
    private readonly IAssistantAiClient _ai;
    private readonly ITranscriptionService _transcription;
    private readonly IAiActionExecutor _executor;
    private readonly IPlanGuard _plan;
    private readonly ITodoService _todos;
    private readonly IInspectionService _inspections;
    private readonly IWeatherService _weather;
    private readonly TimeZoneInfo _tz;
    private readonly int _maxTargets;

    public AiAssistantService(
        IUnitOfWork uow,
        IAccessGuard access,
        ICurrentUser currentUser,
        IAssistantAiClient ai,
        ITranscriptionService transcription,
        IAiActionExecutor executor,
        IPlanGuard plan,
        ITodoService todos,
        IInspectionService inspections,
        IWeatherService weather,
        IConfiguration config)
    {
        _uow = uow;
        _access = access;
        _currentUser = currentUser;
        _ai = ai;
        _transcription = transcription;
        _executor = executor;
        _plan = plan;
        _todos = todos;
        _inspections = inspections;
        _weather = weather;
        _tz = AppTimeZone.Resolve(config);
        _maxTargets = int.TryParse(config["Ai:MaxActionsPerCommand"], out var max) && max > 0 ? max : 50;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<IEnumerable<AssistantSessionSummaryDto>> GetSessionsAsync()
    {
        var userId = RequireUser();
        var sessions = await _uow.AiAssistantSessions.GetByUserAsync(userId);
        return sessions.Select(s => new AssistantSessionSummaryDto(
            s.Id, s.Title, s.UpdatedAt ?? s.CreatedAt, s.CreatedAt, s.BeehiveId, s.Beehive?.Name));
    }

    public async Task<AssistantSessionDetailDto> GetSessionAsync(int id) =>
        ToDetail(await RequireOwnedSessionAsync(id));

    // ── Commands ──────────────────────────────────────────────────────────────

    public async Task<AssistantSessionDetailDto> StartSessionAsync(StartAssistantSessionDto dto)
    {
        var userId = RequireUser();
        await EnsurePlanAllowsInteractionAsync();

        var text = dto.Text.Trim();
        var (assistantTurn, _) = await InterpretAsync(
            text, dto.ApiaryId, dto.BeehiveId, throwIfHiveInaccessible: true, history: []);

        var session = new AiAssistantSession
        {
            UserId    = userId,
            BeehiveId = dto.BeehiveId,
            Title     = TitleFrom(text),
            Turns     =
            {
                new AiAssistantTurn
                {
                    Role       = AiTurnRole.User,
                    Content    = text,
                    Transcript = Trimmed(dto.Transcript),
                },
                assistantTurn,
            },
        };

        await _uow.AiAssistantSessions.AddAsync(session);
        await _uow.SaveChangesAsync();

        return ToDetail((await _uow.AiAssistantSessions.GetWithTurnsAsync(session.Id))!);
    }

    public async Task<AssistantSessionDetailDto> AddTurnAsync(int sessionId, AssistantTurnRequestDto dto)
    {
        RequireUser();
        await EnsurePlanAllowsInteractionAsync();

        var session = await RequireOwnedSessionAsync(sessionId);

        if (session.Turns.Count >= MaxTurns)
            throw new BusinessRuleException("Ovaj razgovor je dostigao maksimalnu dužinu. Započnite novi.");

        var text = dto.Text.Trim();
        var history = session.Turns
            .TakeLast(HistoryWindow)
            .Select(t => new ChatMessage(t.Role == AiTurnRole.User ? "user" : "assistant", t.Content))
            .ToList();

        // A hive named on this turn wins; absent that, a session already scoped to one (from its
        // first turn) still grounds the answer — the same way an Advisor conversation stayed bound
        // to its hive for its whole lifetime.
        var effectiveBeehiveId = dto.BeehiveId ?? session.BeehiveId;
        var (assistantTurn, _) = await InterpretAsync(
            text, dto.ApiaryId, effectiveBeehiveId, throwIfHiveInaccessible: false, history);

        session.Turns.Add(new AiAssistantTurn
        {
            Role       = AiTurnRole.User,
            Content    = text,
            Transcript = Trimmed(dto.Transcript),
        });
        session.Turns.Add(assistantTurn);
        session.UpdatedAt = DateTime.UtcNow;

        await _uow.AiAssistantSessions.UpdateAsync(session);
        await _uow.SaveChangesAsync();

        return ToDetail((await _uow.AiAssistantSessions.GetWithTurnsAsync(session.Id))!);
    }

    public async Task<AssistantConfirmResponseDto> ConfirmAsync(int turnId, ConfirmActionsDto dto)
    {
        var turn = await RequireOwnedTurnAsync(turnId);

        // Double-confirm guard (SPEC-17 §5.5): a phone double-tap must not create two inspections.
        if (turn.Actions.Count == 0 || turn.Actions.Any(a => a.Status != AiActionStatus.Pending))
            throw new BusinessRuleException("Ovaj prijedlog je već obrađen.");

        var requested = dto.Actions.ToDictionary(a => a.Id);

        // Anything the user unchecked is rejected outright.
        foreach (var action in turn.Actions.Where(a => !requested.ContainsKey(a.Id)))
            action.Status = AiActionStatus.Rejected;

        var toRun = turn.Actions.Where(a => requested.ContainsKey(a.Id)).ToList();
        if (toRun.Count == 0)
        {
            await _uow.SaveChangesAsync();
            return new AssistantConfirmResponseDto("Nijedna radnja nije potvrđena.", []);
        }

        // Claim before executing so a racing second request finds nothing Pending and bails. A row
        // left Confirmed with a null ResultEntityId therefore means "claimed but never completed",
        // which is recoverable information — unlike a silently duplicated inspection.
        foreach (var action in toRun)
            action.Status = AiActionStatus.Confirmed;
        await _uow.SaveChangesAsync();

        var results = new List<AssistantExecutionResultDto>();

        foreach (var action in toRun)
        {
            var payload = await BuildConfirmedPayloadAsync(action, requested[action.Id]);

            if (payload is null)
            {
                action.Status = AiActionStatus.Failed;
                action.ErrorMessage = "Odabrani pčelinjak ili košnica nisu dostupni.";
            }
            else
            {
                action.TargetSummary = TargetSummary(payload);
                action.PayloadJson = payload.ToJson();

                var outcome = await _executor.ExecuteAsync(action.Kind, payload);
                if (outcome.Succeeded)
                {
                    action.ResultEntityType = outcome.EntityType;
                    action.ResultEntityId = outcome.EntityId;
                    action.ErrorMessage = null;
                }
                else
                {
                    action.Status = AiActionStatus.Failed;
                    // The column is capped at 500 and this text is not ours to bound — a
                    // ValidationException joins every failed rule into one string. TargetSummary
                    // above is truncated for the same reason; without it here a long message makes
                    // SaveChangesAsync throw, and a batch that already ran comes back as a 500 with
                    // every per-action result lost.
                    action.ErrorMessage = outcome.ErrorMessage is null
                        ? null
                        : Truncate(outcome.ErrorMessage, 500);
                }
            }

            results.Add(new AssistantExecutionResultDto(
                action.Id, action.Status.ToString(), action.TargetSummary,
                action.ResultEntityType, action.ResultEntityId, action.ErrorMessage));
        }

        await _uow.SaveChangesAsync();

        return new AssistantConfirmResponseDto(SummaryMessage(results), results);
    }

    public async Task RejectAsync(int turnId)
    {
        var turn = await RequireOwnedTurnAsync(turnId);

        foreach (var action in turn.Actions.Where(a => a.Status == AiActionStatus.Pending))
            action.Status = AiActionStatus.Rejected;

        await _uow.SaveChangesAsync();
    }

    public async Task<string> TranscribeAsync(Stream audioStream, string fileName)
    {
        // Transcription alone does not consume a command — the resulting text still goes through
        // the quota'd interpret path. Gated as voice input, like the advisor's /transcribe.
        if (_currentUser.OrganizationId is int orgId)
            await _plan.EnsureFeatureAsync(orgId, PlanFeature.VoiceInput);

        var transcript = await _transcription.TranscribeAsync(audioStream, fileName);
        if (string.IsNullOrWhiteSpace(transcript))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["audio"] = ["Nije prepoznat govor u snimku. Pokušajte ponovo."]
            });

        return transcript.Trim();
    }

    public async Task DeleteSessionAsync(int id)
    {
        var userId = RequireUser();
        var session = await _uow.AiAssistantSessions.GetByIdAsync(id);
        if (session is null || session.UserId != userId)
            throw new NotFoundException(nameof(AiAssistantSession), id);

        await _uow.AiAssistantSessions.DeleteAsync(session);
        await _uow.SaveChangesAsync();
    }

    // ── Interpretation ────────────────────────────────────────────────────────

    /// <summary>
    /// One model call plus resolution and pre-flight, producing the assistant turn with its proposal
    /// rows. Nothing is persisted by this method — an AI failure therefore loses none of the user's text.
    /// </summary>
    private async Task<(AiAssistantTurn Turn, AiResolution? Resolution)> InterpretAsync(
        string text, int? pageApiaryId, int? pageBeehiveId, bool throwIfHiveInaccessible,
        IReadOnlyList<ChatMessage> history)
    {
        var apiaries = (await _access.GetAccessibleApiariesAsync())
            .Select(a => new ApiaryRef(a.Id, a.Name)).ToList();
        var hives = (await _access.GetAccessibleBeehivesAsync())
            .Select(h => new HiveRef(h.Id, h.Name, h.LabelNumber, h.ApiaryId)).ToList();

        // SPEC-18: a hive in scope also grounds a Q&A answer in its real data — the same trigger the
        // advisor used, and the same access discipline (throw on a fresh session, degrade silently on
        // a later turn whose access may have since been revoked).
        string? contextBlock = null;
        if (pageBeehiveId is int beehiveId)
        {
            if (throwIfHiveInaccessible)
            {
                await _access.EnsureCanAccessBeehiveAsync(beehiveId);
                contextBlock = await BuildHiveContextBlockAsync(beehiveId);
            }
            else if (await _access.CanAccessBeehiveAsync(beehiveId))
            {
                contextBlock = await BuildHiveContextBlockAsync(beehiveId);
            }
        }

        var system = AssistantPromptBuilder.BuildSystem(apiaries, AppTimeZone.Today(_tz), contextBlock);

        var chat = new List<ChatMessage> { new("system", system) };
        chat.AddRange(history);
        chat.Add(new ChatMessage("user", text));

        var raw = await CallAiAsync(chat);

        var envelope = AiEnvelopeParser.Parse(raw);
        if (envelope is null)
        {
            return (new AiAssistantTurn
            {
                Role         = AiTurnRole.Assistant,
                Content      = "Nisam uspio razumjeti zahtjev. Pokušajte drugačije formulisati.",
                RawModelJson = raw,
            }, null);
        }

        var context = new AiResolutionContext(
            apiaries, hives, pageApiaryId, pageBeehiveId, _maxTargets,
            Todos: await FetchTodoPoolIfNeededAsync(envelope),
            Inspections: await FetchInspectionPoolIfNeededAsync(envelope, hives));
        var resolution = AiTargetResolver.Resolve(envelope, context);
        var candidates = AssistantClarificationBuilder.Build(envelope, resolution, context);

        var turn = new AiAssistantTurn
        {
            Role           = AiTurnRole.Assistant,
            Content        = BuildReply(envelope, resolution),
            RawModelJson   = raw,
            CandidatesJson = SerializeCandidates(candidates),
        };

        foreach (var action in resolution.Actions)
            foreach (var row in await BuildActionRowsAsync(action))
                turn.Actions.Add(row);

        return (turn, resolution);
    }

    /// <summary>
    /// The same per-hive grounding <c>AdvisorService</c> built (SPEC-01): inspections, active diets,
    /// open todos, queen, season yield, latest treatment, latest apiary move, best-effort weather —
    /// rendered by the pure <see cref="HiveContextBuilder"/>. Null on a deleted hive, which degrades
    /// to a general answer rather than failing the turn.
    /// </summary>
    private async Task<string?> BuildHiveContextBlockAsync(int beehiveId)
    {
        var hive = await _uow.Beehives.GetByIdAsync(beehiveId);
        if (hive is null) return null;

        var apiary = await _uow.Apiaries.GetByIdAsync(hive.ApiaryId);
        var apiaryName = apiary?.Name ?? "—";

        var inspections = (await _uow.Inspections.GetByBeehiveIdAsync(beehiveId)).Take(5).ToList();

        var activeDiets = (await _uow.Diets.GetActiveForBeehivesAsync([beehiveId]))
            .GetValueOrDefault(beehiveId, []);

        var openTodos = (await _uow.Todos.GetByBeehiveIdAsync(beehiveId))
            .Where(t => !t.IsCompleted).Take(5).ToList();

        var queen = await _uow.Queens.GetActiveByBeehiveIdAsync(beehiveId);

        var yearly = await _uow.Harvests.GetHiveYearlyTotalsAsync(beehiveId);
        decimal? seasonYield = yearly.TryGetValue(DateTime.UtcNow.Year, out var kg) ? kg : null;

        var latestTreatment = (await _uow.Treatments.GetLatestForBeehivesAsync([beehiveId]))
            .GetValueOrDefault(beehiveId);

        var latestMove = await _uow.ApiaryMoves.GetLatestForApiaryAsync(hive.ApiaryId);
        string? pastureLine = latestMove?.ToPasture is not null
            ? $"{latestMove.ToPasture.Name}, od {latestMove.MovedAt:dd.MM.yyyy}"
            : null;

        string? weatherLine = null;
        if (apiary?.Latitude is double lat && apiary.Longitude is double lon)
        {
            try
            {
                var forecast = await _weather.GetForecastAsync(lat, lon);
                var today = forecast.Daily.FirstOrDefault();
                if (today is not null)
                {
                    var current = forecast.CurrentTemperature.HasValue ? $"{forecast.CurrentTemperature:0}°C trenutno, " : "";
                    weatherLine = $"{current}danas {today.MinTemp:0}–{today.MaxTemp:0}°C";
                }
            }
            catch { /* best-effort, same policy as inspection temperature auto-fill */ }
        }

        return HiveContextBuilder.Build(
            hive, apiaryName, inspections, activeDiets, openTodos, queen, seasonYield,
            latestTreatment, pastureLine, weatherLine);
    }

    /// <summary>
    /// The Phase C search pool for update/complete/delete-todo actions — only fetched when the
    /// envelope actually asks for one of those, so a plain create command never pays for a query it
    /// does not use. Already role-scoped by <see cref="ITodoService.GetAllOpenForCurrentUserAsync"/>,
    /// the same guarantee <see cref="Common.Security.IAccessGuard"/> gives the apiary/hive lists.
    /// </summary>
    private async Task<IReadOnlyList<TodoRef>?> FetchTodoPoolIfNeededAsync(AiEnvelope envelope)
    {
        var needsTodos = envelope.Actions.Any(a =>
            a.Kind is AiActionKind.UpdateTodo or AiActionKind.CompleteTodo or AiActionKind.DeleteTodo);
        if (!needsTodos) return null;

        var todos = await _todos.GetAllOpenForCurrentUserAsync();
        return todos
            .Select(t => new TodoRef(t.Id, t.Title, t.Notes, t.Priority, t.DueDate, t.IsCompleted, t.ApiaryId, t.BeehiveId))
            .ToList();
    }

    /// <summary>
    /// The Phase C search pool for update/delete-inspection actions. A lightweight pre-scan (the same
    /// <see cref="HiveNumberMatcher"/> the resolver itself uses) finds which hives the actions could
    /// possibly mean — this is not a second resolution, just enough to know which hives' inspections
    /// are worth fetching. <c>GetByBeehiveIdAsync</c> is called only for accessible hives, so its own
    /// internal access check can never throw here.
    /// </summary>
    private async Task<IReadOnlyList<InspectionRef>?> FetchInspectionPoolIfNeededAsync(
        AiEnvelope envelope, IReadOnlyList<HiveRef> hives)
    {
        var relevant = envelope.Actions
            .Where(a => a.Kind is AiActionKind.UpdateInspection or AiActionKind.DeleteInspection)
            .ToList();
        if (relevant.Count == 0) return null;

        var hiveIds = new HashSet<int>();
        foreach (var action in relevant)
        foreach (var number in action.Hives.Numbers)
        {
            var canonical = HiveNumberMatcher.Normalize(number);
            if (canonical is null) continue;
            foreach (var hive in hives.Where(h => HiveNumberMatcher.Matches(h.LabelNumber, h.Name, canonical)))
                hiveIds.Add(hive.Id);
        }
        if (hiveIds.Count == 0) return [];

        var result = new List<InspectionRef>();
        foreach (var hiveId in hiveIds)
        {
            var inspections = await _inspections.GetByBeehiveIdAsync(hiveId);
            result.AddRange(inspections.Select(i =>
                new InspectionRef(i.Id, i.Date, i.HoneyLevel, i.BroodStatus, i.Notes, i.BeehiveId)));
        }
        return result;
    }

    /// <summary>
    /// Turns one resolved action into its proposal rows — one per target, or a single unresolved row
    /// the user completes with the card's dropdown. Pre-flight drops what the caller could not do
    /// anyway, so a card that is certain to fail is never offered.
    /// </summary>
    private async Task<List<AiAssistantAction>> BuildActionRowsAsync(AiResolvedAction action)
    {
        var rows = new List<AiAssistantAction>();

        foreach (var target in action.Targets)
        {
            // An apiary-level todo is a management action; a Beekeeper can see the apiary but not
            // create one there (TodoService.CreateAsync). Checking now beats a card that 403s later.
            if (target.BeehiveId is null && !await _access.CanManageApiaryAsync(target.ApiaryId))
                continue;

            rows.Add(NewRow(action, new AiActionPayload(
                target.ApiaryId, target.ApiaryName, target.BeehiveId, target.BeehiveName,
                AiResolutionIssue.None, [], action.Fields,
                target.ExistingEntityId, target.ExistingEntityType, target.ExistingSummary, target.PreviousFields)));
        }

        // Nothing resolved, but the user still said something — offer one row to complete rather than
        // discarding the fields they dictated.
        if (rows.Count == 0 && action.Issue != AiResolutionIssue.None)
        {
            rows.Add(NewRow(action, new AiActionPayload(
                null, null, null, null, action.Issue, action.UnresolvedNumbers, action.Fields)));
        }

        return rows;
    }

    private static AiAssistantAction NewRow(AiResolvedAction action, AiActionPayload payload) => new()
    {
        Kind          = action.Kind,
        Status        = AiActionStatus.Pending,
        PayloadJson   = payload.ToJson(),
        TargetSummary = TargetSummary(payload),
    };

    /// <summary>
    /// Rebuilds the payload from the confirmation request. The card is editable, so these values are
    /// untrusted (SPEC-17 §5.3): the target is re-checked against what the caller can actually reach,
    /// and the executor re-runs the validators regardless of what was proposed earlier.
    /// </summary>
    private async Task<AiActionPayload?> BuildConfirmedPayloadAsync(AiAssistantAction action, ConfirmActionItemDto item)
    {
        var stored = AiActionPayload.FromJson(action.PayloadJson);
        var fields = ToFields(item.Fields, stored?.Fields);

        // Phase C: an update/complete/delete's target is an existing record the resolver already
        // found — fixed at propose time, never re-picked from the confirm request. Only the edited
        // fields flow through; the executor re-fetches the record and re-checks access before
        // touching it, so this is not a shortcut around SPEC-17 §5.3.
        if (stored?.ExistingEntityId is not null)
            return stored with { Fields = fields };

        var beehiveId = item.BeehiveId ?? stored?.BeehiveId;
        var apiaryId  = item.ApiaryId  ?? stored?.ApiaryId;

        if (beehiveId is int hiveId)
        {
            var hive = (await _access.GetAccessibleBeehivesAsync()).FirstOrDefault(h => h.Id == hiveId);
            if (hive is null) return null;

            var apiary = (await _access.GetAccessibleApiariesAsync()).FirstOrDefault(a => a.Id == hive.ApiaryId);
            return new AiActionPayload(
                hive.ApiaryId, apiary?.Name ?? "—", hive.Id, hive.Name,
                AiResolutionIssue.None, [], fields);
        }

        if (apiaryId is int targetApiaryId)
        {
            var apiary = (await _access.GetAccessibleApiariesAsync()).FirstOrDefault(a => a.Id == targetApiaryId);
            if (apiary is null) return null;
            if (!await _access.CanManageApiaryAsync(apiary.Id)) return null;

            return new AiActionPayload(apiary.Id, apiary.Name, null, null, AiResolutionIssue.None, [], fields);
        }

        return null;
    }

    // ── AI call ───────────────────────────────────────────────────────────────

    private async Task<string> CallAiAsync(IReadOnlyList<ChatMessage> chat)
    {
        try
        {
            var raw = await _ai.SendAsync(chat);
            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("Empty AI reply.");
            return raw.Trim();
        }
        catch (Exception)
        {
            // Callers persist nothing before this returns, so a failure loses none of the user's text.
            throw new BusinessRuleException("AI servis trenutno nije dostupan. Pokušajte ponovo za koji trenutak.");
        }
    }

    // ── Presentation ──────────────────────────────────────────────────────────

    private string BuildReply(AiEnvelope envelope, AiResolution resolution)
    {
        if (resolution.ExceededCeiling)
            return $"Ovaj zahtjev bi kreirao {resolution.TotalTargets} zapisa, a najviše je dozvoljeno " +
                   $"{_maxTargets} odjednom. Suzite naredbu (npr. navedite košnice pojedinačno).";

        var reply = envelope.Reply.Trim();

        if (envelope.Needs is { } needs && !string.IsNullOrWhiteSpace(needs.Question))
            reply = string.IsNullOrEmpty(reply) ? needs.Question : $"{reply} {needs.Question}";

        if (!string.IsNullOrEmpty(reply)) return reply;

        return resolution.Actions.Count == 0
            ? "Nisam prepoznao nijednu radnju. Recite npr. „Pregledana košnica 2, med srednji“."
            : "Evo šta sam razumio — provjerite i potvrdite.";
    }

    private static string SummaryMessage(IReadOnlyList<AssistantExecutionResultDto> results)
    {
        var ok = results.Count(r => r.Status == nameof(AiActionStatus.Confirmed));
        var failed = results.Count - ok;

        if (failed == 0) return ok == 1 ? "Radnja je izvršena." : $"Izvršeno {ok} radnji.";
        if (ok == 0) return ok + failed == 1 ? "Radnja nije izvršena." : "Nijedna radnja nije izvršena.";
        return $"Izvršeno {ok} od {ok + failed} radnji — ostale nisu uspjele.";
    }

    private static string TargetSummary(AiActionPayload payload)
    {
        if (payload.ExistingSummary is not null)
            return Truncate(payload.ExistingSummary, 200);
        if (payload.BeehiveName is not null && payload.ApiaryName is not null)
            return Truncate($"{payload.ApiaryName} · {payload.BeehiveName}", 200);
        if (payload.ApiaryName is not null)
            return Truncate(payload.ApiaryName, 200);
        return "Nije određeno";
    }

    /// <summary>
    /// Merges the card's edits over what was proposed. Everything here is untrusted (SPEC-17 §5.3) —
    /// including <c>TargetBeehiveId</c>, which the merge card lets the user re-pick:
    /// <c>BeehiveMergeService</c> re-checks both hives against <see cref="IAccessGuard"/> before it
    /// writes, so a swapped-in id fails there rather than being trusted here.
    /// </summary>
    private static AiActionFields ToFields(AssistantFieldsDto edited, AiActionFields? stored) => new(
        Date:        edited.Date        ?? stored?.Date,
        HoneyLevel:  edited.HoneyLevel  ?? stored?.HoneyLevel,
        BroodStatus: Trimmed(edited.BroodStatus) ?? stored?.BroodStatus,
        Notes:       Trimmed(edited.Notes)       ?? stored?.Notes,
        Title:       Trimmed(edited.Title)       ?? stored?.Title,
        Priority:    edited.Priority    ?? stored?.Priority,
        DueDate:     edited.DueDate     ?? stored?.DueDate,
        TargetHive:  stored?.TargetHive,
        TargetBeehiveId:   edited.TargetBeehiveId   ?? stored?.TargetBeehiveId,
        TargetBeehiveName: edited.TargetBeehiveName ?? stored?.TargetBeehiveName,
        QueenOutcome:      edited.QueenOutcome      ?? stored?.QueenOutcome,
        MergeReason:       edited.MergeReason       ?? stored?.MergeReason);

    private static AssistantSessionDetailDto ToDetail(AiAssistantSession s)
    {
        var turns = s.Turns.Select(ToTurnDto).ToList();

        // A clarification is a "what to do right now" affordance. Keeping it on an already-superseded
        // turn would let the user answer a question a later message has already moved past.
        for (var i = 0; i < turns.Count - 1; i++)
            if (turns[i].Candidates.Count > 0)
                turns[i] = turns[i] with { Candidates = [] };

        return new(s.Id, s.Title, s.UpdatedAt ?? s.CreatedAt, s.CreatedAt, s.BeehiveId, s.Beehive?.Name, turns);
    }

    private static AssistantTurnDto ToTurnDto(AiAssistantTurn t) => new(
        t.Id, t.Role.ToString(), t.Content, t.Transcript, t.CreatedAt,
        t.Actions.Select(ToActionDto).ToList(),
        DeserializeCandidates(t.CandidatesJson));

    private static AssistantActionDto ToActionDto(AiAssistantAction a)
    {
        var payload = AiActionPayload.FromJson(a.PayloadJson);
        var fields = payload?.Fields ?? new AiActionFields();

        return new AssistantActionDto(
            a.Id,
            a.Kind.ToString(),
            BsLabels.Label(a.Kind),
            a.Status.ToString(),
            a.TargetSummary,
            (payload?.Issue ?? AiResolutionIssue.None).ToString(),
            payload?.UnresolvedHiveNumbers ?? [],
            payload?.ApiaryId,
            payload?.ApiaryName,
            payload?.BeehiveId,
            payload?.BeehiveName,
            ToFieldsDto(fields),
            a.ResultEntityType,
            a.ResultEntityId,
            a.ErrorMessage,
            IsDestructive(a.Kind),
            payload?.PreviousFields is { } previous ? ToFieldsDto(previous) : null);
    }

    /// <summary>
    /// Phase C, SPEC-17 D5/§7.3: "update and delete" — literally, not "complete" too. Checking a todo
    /// off is a one-tap, instantly-reversible toggle everywhere else in this app (the plain todo
    /// checkbox asks nothing), so holding it to the same bar as overwriting or destroying a record
    /// would be a stricter rule than the rest of the UI already applies to the identical action.
    /// </summary>
    /// <remarks>
    /// <see cref="AiActionKind.MergeBeehive"/> joins them (SPEC-19 §8): it takes a hive out of the
    /// apiary for good, which is further than any delete here goes — the 24-hour undo is a safety net
    /// for a misclick, not a reason to ask less.
    /// </remarks>
    private static bool IsDestructive(AiActionKind kind) => kind is
        AiActionKind.UpdateTodo or AiActionKind.UpdateInspection or
        AiActionKind.DeleteTodo or AiActionKind.DeleteInspection or
        AiActionKind.MergeBeehive;

    private static AssistantFieldsDto ToFieldsDto(AiActionFields fields) => new()
    {
        Date              = fields.Date,
        HoneyLevel        = fields.HoneyLevel,
        BroodStatus       = fields.BroodStatus,
        Notes             = fields.Notes,
        Title             = fields.Title,
        Priority          = fields.Priority,
        DueDate           = fields.DueDate,
        TargetBeehiveId   = fields.TargetBeehiveId,
        TargetBeehiveName = fields.TargetBeehiveName,
        QueenOutcome      = fields.QueenOutcome,
        MergeReason       = fields.MergeReason,
    };

    // ── Guards & helpers ──────────────────────────────────────────────────────

    private async Task<AiAssistantSession> RequireOwnedSessionAsync(int id)
    {
        var userId = RequireUser();
        var session = await _uow.AiAssistantSessions.GetWithTurnsAsync(id);
        if (session is null || session.UserId != userId)
            throw new NotFoundException(nameof(AiAssistantSession), id);
        return session;
    }

    private async Task<AiAssistantTurn> RequireOwnedTurnAsync(int turnId)
    {
        var userId = RequireUser();
        var turn = await _uow.AiAssistantSessions.GetTurnWithActionsAsync(turnId);
        if (turn is null || turn.Session.UserId != userId)
            throw new NotFoundException(nameof(AiAssistantTurn), turnId);
        return turn;
    }

    private async Task EnsurePlanAllowsInteractionAsync()
    {
        if (_currentUser.OrganizationId is int orgId)
            await _plan.EnsureAiInteractionAsync(orgId);
    }

    private int RequireUser() =>
        _currentUser.UserId ?? throw new ForbiddenAccessException("Morate biti prijavljeni za korištenje asistenta.");

    private static string TitleFrom(string text)
    {
        var t = text.Trim();
        return t.Length <= 60 ? t : t[..60].TrimEnd() + "…";
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    private static string? Trimmed(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static string? SerializeCandidates(IReadOnlyList<AssistantCandidate> candidates) =>
        candidates.Count == 0 ? null : JsonSerializer.Serialize(candidates);

    /// <summary>Never throws on a row written by older code — an unreadable list just yields no buttons.</summary>
    private static IReadOnlyList<AssistantCandidateDto> DeserializeCandidates(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<AssistantCandidateDto>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
