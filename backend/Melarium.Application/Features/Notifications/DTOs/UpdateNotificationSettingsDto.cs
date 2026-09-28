using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Notifications.DTOs;

public record UpdateNotificationSettingsDto(
    EmailNotificationMode EmailMode,
    bool NormalAlertsInApp,
    bool InfoAlertsInApp);
