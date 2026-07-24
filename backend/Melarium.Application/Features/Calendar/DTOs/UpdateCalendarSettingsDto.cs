namespace Melarium.Application.Features.Calendar.DTOs;

public record UpdateCalendarSettingsDto(
    bool FeedEnabled,
    bool SyncFeedings,
    bool SyncTodos,
    bool SyncTreatments,
    bool SyncInspections,
    bool DailyAgendaEnabled);
