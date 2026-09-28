using System.Globalization;
using Melarium.Application.Common;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Models;
using Melarium.Application.Features.Calendar;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Reminders;

/// <summary>
/// The 08:00 morning run. For each user it creates the in-app agenda of today's obligations (SPEC-11)
/// and sends <b>one</b> morning e-mail (SPEC-29): the Normal alerts the scan produced since yesterday
/// morning plus today's obligations. Before SPEC-29 every alert was its own e-mail and the agenda a
/// separate one; Critical alerts still go out on their own, at the moment of the scan.
/// </summary>
public class DailyAgendaService : IDailyAgendaService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly ICalendarObligationService _obligations;
    private readonly IEmailQueue _emailQueue;
    private readonly INotificationPolicy _policy;
    private readonly IConfiguration _config;
    private readonly TimeProvider _time;

    public DailyAgendaService(
        IUnitOfWork uow,
        INotificationService notifications,
        ICalendarObligationService obligations,
        IEmailQueue emailQueue,
        INotificationPolicy policy,
        IConfiguration config,
        TimeProvider time)
    {
        _uow           = uow;
        _notifications = notifications;
        _obligations   = obligations;
        _emailQueue    = emailQueue;
        _policy        = policy;
        _config        = config;
        _time          = time;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var agendaEnabled = GetBool("Reminders:DailyAgenda:Enabled", true);

        var now     = _time.GetUtcNow().UtcDateTime;
        var tz      = AppTimeZone.Resolve(_config);
        var today   = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, tz));
        var dedupId = int.Parse(today.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var since   = now.AddHours(-20);

        var users             = (await _uow.Users.GetAllAsync()).ToList();
        var calendarByUser    = (await _uow.CalendarSettings.GetAllAsync()).ToDictionary(s => s.UserId);
        var preferencesByUser = (await _uow.NotificationSettings.GetAllAsync()).ToDictionary(s => s.UserId);

        // Everything the scan left for the morning, read once. The window covers exactly one scan: it
        // runs at Alerts:ScanHourUtc, before 08:00 local in both CET and CEST.
        var alertsByUser = (await _uow.Notifications.GetNormalSinceAsync(now.AddHours(-24), _policy.MorningDigestTypes))
            .GroupBy(n => n.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // SystemAdmin has no personal beekeeping schedule (no organization scope).
            if (user.Role == UserRole.SystemAdmin) continue;

            IReadOnlyList<CalendarObligation> items = [];
            calendarByUser.TryGetValue(user.Id, out var calendar);

            if (agendaEnabled && calendar is not { DailyAgendaEnabled: false })
            {
                var cats = calendar is null
                    ? CalendarCategories.All
                    : new CalendarCategories(calendar.SyncFeedings, calendar.SyncTodos, calendar.SyncTreatments, calendar.SyncInspections);

                var ctx = new CalendarUserContext(user.Id, user.Role, user.OrganizationId, user.ApiaryId);
                items = await _obligations.GatherAsync(ctx, today, today, cats);

                // Deduped per calendar day (dedupId = yyyyMMdd), so a re-run the same morning is a no-op —
                // for the e-mail as well: an agenda already sent today means this morning was handled.
                if (items.Count > 0)
                {
                    if (await _uow.Notifications.ExistsRecentAsync(user.Id, NotificationType.DailyAgenda, dedupId, since))
                        continue;

                    await _notifications.NotifyAsync(user.Id, "Današnje obaveze", AgendaMessage(today, items),
                        NotificationType.DailyAgenda, dedupId, "DailyAgenda");
                }
            }

            var alerts = alertsByUser.TryGetValue(user.Id, out var own) ? own : [];
            var mode = preferencesByUser.TryGetValue(user.Id, out var prefs) ? prefs.EmailMode : EmailNotificationMode.All;

            // Only "all" wants the morning e-mail: the others asked for Critical (sent at scan time) or nothing.
            if (mode != EmailNotificationMode.All || (items.Count == 0 && alerts.Count == 0)) continue;

            _emailQueue.Enqueue(QueuedEmail.ForUser(user.Id, MorningEmail.Compose(today, items, alerts, user.FirstName)));
        }
    }

    private static string AgendaMessage(DateOnly today, IReadOnlyList<CalendarObligation> items)
    {
        var dateStr = today.ToString("dd.MM.", CultureInfo.InvariantCulture);
        var list    = string.Join("; ", items.Select(i => i.Title));
        return $"Dobro jutro! Danas ({dateStr}) imaš {items.Count} {ObligationWord(items.Count)}: {list}.";
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
