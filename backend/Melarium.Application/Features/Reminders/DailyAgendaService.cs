using System.Globalization;
using Melarium.Application.Common;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Calendar;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Reminders;

/// <summary>
/// The reliable 08:00 reminder that a subscribed calendar's own alarms can't guarantee (Google ignores
/// VALARM on ICS subscriptions). For each opted-in user it gathers today's obligations and sends one
/// consolidated notification, deduped per day so a re-run never double-sends.
/// </summary>
public class DailyAgendaService : IDailyAgendaService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly ICalendarObligationService _obligations;
    private readonly IConfiguration _config;

    public DailyAgendaService(
        IUnitOfWork uow,
        INotificationService notifications,
        ICalendarObligationService obligations,
        IConfiguration config)
    {
        _uow           = uow;
        _notifications = notifications;
        _obligations   = obligations;
        _config        = config;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (!GetBool("Reminders:DailyAgenda:Enabled", true)) return;

        var tz      = AppTimeZone.Resolve(_config);
        var today   = AppTimeZone.Today(tz);
        var dedupId = int.Parse(today.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var since   = DateTime.UtcNow.AddHours(-20);

        var users          = (await _uow.Users.GetAllAsync()).ToList();
        var settingsByUser = (await _uow.CalendarSettings.GetAllAsync()).ToDictionary(s => s.UserId);

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // SystemAdmin has no personal beekeeping schedule (no organization scope).
            if (user.Role == UserRole.SystemAdmin) continue;

            settingsByUser.TryGetValue(user.Id, out var s);
            if (s is { DailyAgendaEnabled: false }) continue;

            var cats = s is null
                ? CalendarCategories.All
                : new CalendarCategories(s.SyncFeedings, s.SyncTodos, s.SyncTreatments, s.SyncInspections);

            var ctx   = new CalendarUserContext(user.Id, user.Role, user.OrganizationId, user.ApiaryId);
            var items = await _obligations.GatherAsync(ctx, today, today, cats);
            if (items.Count == 0) continue;

            // Deduped per calendar day (dedupId = yyyyMMdd) so a re-run the same morning is a no-op.
            if (await _uow.Notifications.ExistsRecentAsync(user.Id, NotificationType.DailyAgenda, dedupId, since))
                continue;

            var (title, message) = Compose(today, items);
            await _notifications.NotifyAsync(user.Id, title, message, NotificationType.DailyAgenda, dedupId, "DailyAgenda");
        }
    }

    private static (string Title, string Message) Compose(DateOnly today, IReadOnlyList<CalendarObligation> items)
    {
        var dateStr = today.ToString("dd.MM.", CultureInfo.InvariantCulture);
        var list    = string.Join("; ", items.Select(i => i.Title));
        var message = $"Dobro jutro! Danas ({dateStr}) imaš {items.Count} {ObligationWord(items.Count)}: {list}.";
        return ("Današnje obaveze", message);
    }

    /// <summary>Bosnian plural of "obaveza": 1 → obavezu, 2–4 → obaveze, else obaveza (with 11–14 exception).</summary>
    private static string ObligationWord(int n)
    {
        var mod100 = n % 100;
        var mod10  = n % 10;
        if (mod10 == 1 && mod100 != 11) return "obavezu";
        if (mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14) return "obaveze";
        return "obaveza";
    }

    private bool GetBool(string key, bool fallback) => bool.TryParse(_config[key], out var v) ? v : fallback;
}
