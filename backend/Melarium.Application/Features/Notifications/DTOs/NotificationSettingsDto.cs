using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Notifications.DTOs;

/// <summary>The caller's own notification preferences (SPEC-29). Defaults when never saved.</summary>
public record NotificationSettingsDto(
    EmailNotificationMode EmailMode,
    bool NormalAlertsInApp,
    bool InfoAlertsInApp);
