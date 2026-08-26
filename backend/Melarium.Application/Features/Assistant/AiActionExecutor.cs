using FluentValidation;
using Melarium.Application.Common;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Features.Inspections;
using Melarium.Application.Features.Inspections.DTOs;
using Melarium.Application.Features.Todos;
using Melarium.Application.Features.Todos.DTOs;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ValidationException = Melarium.Application.Common.Exceptions.ValidationException;

namespace Melarium.Application.Features.Assistant;

/// <summary>Where one confirmed action ended up.</summary>
public sealed record AiActionOutcome(bool Succeeded, string? EntityType, int? EntityId, string? ErrorMessage);

/// <summary>
/// Executes a confirmed action by building the same DTO a form would post and calling the
/// <b>existing</b> application service (SPEC-17 §5.1). That is the whole design: access rules, plan
/// limits, the automatic weather temperature and the todo notification cascade all come along for
/// free, and cannot be forgotten. Never write to a repository from here.
/// </summary>
public class AiActionExecutor : IAiActionExecutor
{
    private readonly IInspectionService _inspections;
    private readonly ITodoService _todos;
    private readonly IValidator<CreateInspectionDto> _inspectionValidator;
    private readonly IValidator<CreateTodoDto> _todoValidator;
    private readonly IValidator<UpdateTodoDto> _todoUpdateValidator;
    private readonly IValidator<UpdateInspectionDto> _inspectionUpdateValidator;
    private readonly TimeZoneInfo _tz;
    private readonly ILogger<AiActionExecutor> _logger;

    public AiActionExecutor(
        IInspectionService inspections,
        ITodoService todos,
        IValidator<CreateInspectionDto> inspectionValidator,
        IValidator<CreateTodoDto> todoValidator,
        IValidator<UpdateTodoDto> todoUpdateValidator,
        IValidator<UpdateInspectionDto> inspectionUpdateValidator,
        IConfiguration config,
        ILogger<AiActionExecutor> logger)
    {
        _inspections = inspections;
        _todos = todos;
        _inspectionValidator = inspectionValidator;
        _todoValidator = todoValidator;
        _todoUpdateValidator = todoUpdateValidator;
        _inspectionUpdateValidator = inspectionUpdateValidator;
        _tz = AppTimeZone.Resolve(config);
        _logger = logger;
    }

    public async Task<AiActionOutcome> ExecuteAsync(AiActionKind kind, AiActionPayload payload)
    {
        try
        {
            return kind switch
            {
                AiActionKind.CreateInspection => await CreateInspectionAsync(payload),
                AiActionKind.CreateTodo       => await CreateTodoAsync(payload),
                AiActionKind.UpdateTodo       => await UpdateTodoAsync(payload),
                AiActionKind.CompleteTodo     => await CompleteTodoAsync(payload),
                AiActionKind.UpdateInspection => await UpdateInspectionAsync(payload),
                AiActionKind.DeleteTodo       => await DeleteTodoAsync(payload),
                AiActionKind.DeleteInspection => await DeleteInspectionAsync(payload),
                _                             => Failed("Ova radnja još nije podržana."),
            };
        }
        // The application's own exceptions already carry a Bosnian, user-facing message.
        catch (Exception ex) when (ex is ForbiddenAccessException or NotFoundException
                                      or PlanLimitException or BusinessRuleException or ValidationException)
        {
            return Failed(UserMessage(ex));
        }
        catch (Exception ex)
        {
            // An unexpected failure must not take down the rest of the batch, but it must be visible.
            _logger.LogError(ex, "AI assistant action {Kind} failed unexpectedly", kind);
            return Failed("Radnja nije uspjela zbog neočekivane greške.");
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private async Task<AiActionOutcome> CreateInspectionAsync(AiActionPayload payload)
    {
        if (payload.BeehiveId is not int beehiveId)
            return Failed("Nije odabrana košnica.");

        var dto = new CreateInspectionDto
        {
            BeehiveId   = beehiveId,
            Date        = ToInspectionDate(payload.Fields.Date),
            HoneyLevel  = payload.Fields.HoneyLevel ?? HoneyLevel.Medium,
            BroodStatus = payload.Fields.BroodStatus,
            Notes       = payload.Fields.Notes,
        };

        Validate(_inspectionValidator, dto);

        var created = await _inspections.CreateAsync(dto);
        return new AiActionOutcome(true, nameof(Domain.Entities.Inspection), created.Id, null);
    }

    private async Task<AiActionOutcome> CreateTodoAsync(AiActionPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Fields.Title))
            return Failed("Zadatak nema naslov.");

        if (payload.BeehiveId is null && payload.ApiaryId is null)
            return Failed("Nije odabran pčelinjak ni košnica.");

        var dto = new CreateTodoDto
        {
            Title     = payload.Fields.Title!.Trim(),
            Notes     = payload.Fields.Notes,
            Priority  = payload.Fields.Priority ?? TodoPriority.Medium,
            DueDate   = UtcMidnight(payload.Fields.DueDate),
            // The validator requires exactly one target, so a hive-scoped todo must not also carry
            // its apiary — the resolver knows both, and the hive is the more specific of the two.
            BeehiveId = payload.BeehiveId,
            ApiaryId  = payload.BeehiveId is null ? payload.ApiaryId : null,
        };

        Validate(_todoValidator, dto);

        var created = await _todos.CreateAsync(dto);
        return new AiActionOutcome(true, nameof(Domain.Entities.Todo), created.Id, null);
    }

    // ── Phase C: existing-record actions ────────────────────────────────────────
    //
    // `TodoService`/`InspectionService` do their own `EnsureCanManage*`/`EnsureCanAccessBeehiveAsync`
    // check on the *current* record before touching it — the resolver's target id is a hint about
    // which record, never a bypass of that check. A record that became inaccessible between propose
    // and confirm still fails here exactly as it would from the normal edit form (SPEC-17 §5.3).
    //
    // `UpdateTodoDto`/`UpdateInspectionDto` replace the whole entity (AutoMapper `Map(dto, entity)`),
    // so every field must be populated — the AI's `null` fields fall back to the record's current
    // value rather than being sent as an accidental clear.

    private async Task<AiActionOutcome> UpdateTodoAsync(AiActionPayload payload)
    {
        if (payload.ExistingEntityId is not int todoId) return Failed("Zadatak nije pronađen.");

        var current = await _todos.GetByIdAsync(todoId);
        var dto = new UpdateTodoDto
        {
            Title        = Trimmed(payload.Fields.Title) ?? current.Title,
            Notes        = payload.Fields.Notes ?? current.Notes,
            DueDate      = UtcMidnight(payload.Fields.DueDate) ?? current.DueDate,
            Priority     = payload.Fields.Priority ?? current.Priority,
            IsCompleted  = current.IsCompleted, // a status change goes through CompleteTodo, not this
            AssignedToId = current.AssignedToId,
        };

        Validate(_todoUpdateValidator, dto);

        var updated = await _todos.UpdateAsync(todoId, dto);
        return new AiActionOutcome(true, nameof(Domain.Entities.Todo), updated.Id, null);
    }

    private async Task<AiActionOutcome> CompleteTodoAsync(AiActionPayload payload)
    {
        if (payload.ExistingEntityId is not int todoId) return Failed("Zadatak nije pronađen.");

        var current = await _todos.GetByIdAsync(todoId);
        if (current.IsCompleted) return Failed("Zadatak je već označen kao završen.");

        var dto = new UpdateTodoDto
        {
            Title = current.Title, Notes = current.Notes, DueDate = current.DueDate,
            Priority = current.Priority, IsCompleted = true, AssignedToId = current.AssignedToId,
        };

        Validate(_todoUpdateValidator, dto);

        var updated = await _todos.UpdateAsync(todoId, dto);
        return new AiActionOutcome(true, nameof(Domain.Entities.Todo), updated.Id, null);
    }

    private async Task<AiActionOutcome> DeleteTodoAsync(AiActionPayload payload)
    {
        if (payload.ExistingEntityId is not int todoId) return Failed("Zadatak nije pronađen.");

        await _todos.DeleteAsync(todoId);
        return new AiActionOutcome(true, nameof(Domain.Entities.Todo), todoId, null);
    }

    private async Task<AiActionOutcome> UpdateInspectionAsync(AiActionPayload payload)
    {
        if (payload.ExistingEntityId is not int inspectionId) return Failed("Pregled nije pronađen.");

        var current = await _inspections.GetByIdAsync(inspectionId);
        var dto = new UpdateInspectionDto
        {
            // Neither the hive nor the date changes here — the prompt uses `fields.date` to say WHICH
            // inspection is meant, never as a "move it to a new day" instruction (only honeyLevel,
            // broodStatus and notes are the editable new values for this action).
            BeehiveId   = current.BeehiveId,
            Date        = current.Date,
            HoneyLevel  = payload.Fields.HoneyLevel ?? current.HoneyLevel,
            BroodStatus = payload.Fields.BroodStatus ?? current.BroodStatus,
            Notes       = payload.Fields.Notes ?? current.Notes,
        };

        Validate(_inspectionUpdateValidator, dto);

        var updated = await _inspections.UpdateAsync(inspectionId, dto);
        return new AiActionOutcome(true, nameof(Domain.Entities.Inspection), updated.Id, null);
    }

    private async Task<AiActionOutcome> DeleteInspectionAsync(AiActionPayload payload)
    {
        if (payload.ExistingEntityId is not int inspectionId) return Failed("Pregled nije pronađen.");

        await _inspections.DeleteAsync(inspectionId);
        return new AiActionOutcome(true, nameof(Domain.Entities.Inspection), inspectionId, null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the same validator the controller runs (SPEC-17 §5.2). Validation lives in the controllers
    /// in this codebase, so without this an AI-authored record would pass fewer checks than a typed one.
    /// </summary>
    private static void Validate<T>(IValidator<T> validator, T dto)
    {
        var result = validator.Validate(dto);
        if (result.IsValid) return;

        throw new ValidationException(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    }

    /// <summary>
    /// Turns the resolved local calendar date into the value the form would have posted: midnight,
    /// stored verbatim as UTC.
    ///
    /// The clamp matters between local midnight and 01:00/02:00, when today's local midnight is still
    /// in the future by UTC and <c>CreateInspectionValidator</c>'s "date cannot be in the future" rule
    /// would reject it. Clamping to <c>UtcNow</c> both passes that rule and renders as the correct
    /// local day in the UI, which formats the instant in local time.
    /// </summary>
    private DateTime ToInspectionDate(DateOnly? date)
    {
        var midnight = UtcMidnight(date ?? AppTimeZone.Today(_tz));
        var now = DateTime.UtcNow;
        return midnight > now ? now : midnight;
    }

    /// <summary>
    /// Midnight on a calendar date, as UTC. <c>DateOnly.ToDateTime</c> yields
    /// <see cref="DateTimeKind.Unspecified"/>, which Npgsql refuses to write to a <c>timestamptz</c>
    /// column — and these were the only three places in the application that produced such a value,
    /// which is why AI assistant confirmation was the one flow that hit it (ADR-037). The model-wide
    /// converter now catches this too; marking the Kind here keeps the value honest before it ever
    /// reaches a DTO, a validator, or a comparison.
    /// </summary>
    private static DateTime UtcMidnight(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static DateTime? UtcMidnight(DateOnly? date) =>
        date is { } d ? UtcMidnight(d) : null;

    private static string UserMessage(Exception ex) => ex switch
    {
        ForbiddenAccessException => "Nemate ovlaštenje za ovu radnju.",
        NotFoundException        => "Zapis više ne postoji.",
        ValidationException v    => string.Join(" ", v.Errors.SelectMany(e => e.Value).Distinct()),
        _                        => ex.Message,
    };

    private static AiActionOutcome Failed(string message) => new(false, null, null, message);

    private static string? Trimmed(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }
}
