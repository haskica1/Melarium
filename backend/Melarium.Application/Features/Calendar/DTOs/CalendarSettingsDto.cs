namespace Melarium.Application.Features.Calendar.DTOs;

public record CalendarSettingsDto(
    bool FeedEnabled,
    bool SyncFeedings,
    bool SyncTodos,
    bool SyncTreatments,
    bool SyncInspections,
    bool DailyAgendaEnabled);
