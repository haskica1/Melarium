using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Weather;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Alerts;

/// <summary>
/// Rule-based proactive alerts (SPEC-04 Part A). Each rule is individually toggleable via
/// <c>Alerts:{RuleName}:Enabled</c> (all default true) and deduplicated against the existing
/// notifications table so re-running the scan never produces duplicates.
/// </summary>
public class AlertRuleService : IAlertRuleService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly IWeatherService _weather;
    private readonly IConfiguration _config;

    public AlertRuleService(
        IUnitOfWork uow,
        INotificationService notifications,
        IWeatherService weather,
        IConfiguration config)
    {
        _uow = uow;
        _notifications = notifications;
        _weather = weather;
        _config = config;
    }

    public async Task RunDailyScanAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var staleDays = GetInt("Alerts:StaleInspectionDays", 21);

        var staleEnabled = GetBool("Alerts:StaleInspection:Enabled", true);
        var dropEnabled  = GetBool("Alerts:HoneyLevelDrop:Enabled", true);
        var frostEnabled = GetBool("Alerts:FrostWarning:Enabled", true);
        var queenEnabled = GetBool("Alerts:OldQueen:Enabled", true);

        var stripsEnabled  = GetBool("Alerts:StripsLeftIn:Enabled", true);
        var karencaEnabled = GetBool("Alerts:KarencaEnded:Enabled", true);
        var stripDays      = GetInt("Alerts:StripRemovalDays", 42);

        var feedingEnabled = GetBool("Alerts:FeedingOverdue:Enabled", true);
        var feedingDays    = GetInt("Alerts:FeedingOverdueDays", 2);

        var roundOverdueEnabled = GetBool("Alerts:TreatmentRoundOverdue:Enabled", true);
        var roundOverdueDays    = GetInt("Alerts:TreatmentRoundOverdueDays", 2);

        if (GetBool("Alerts:PlanExpiring:Enabled", true))
            await ApplyPlanExpiringAsync(now);

        var apiaries = (await _uow.Apiaries.GetAllAsync()).ToList();

        foreach (var apiary in apiaries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hives = (await _uow.Beehives.GetByApiaryIdAsync(apiary.Id)).ToList();
            if (hives.Count == 0)
            {
                if (frostEnabled) await ApplyFrostAsync(apiary);
                continue;
            }

            var hiveIds = hives.Select(h => h.Id).ToList();

            var inspections = (await _uow.Inspections.FindAsync(i => hiveIds.Contains(i.BeehiveId))).ToList();
            var byHive = inspections
                .GroupBy(i => i.BeehiveId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.Date).ToList());

            var activeQueens = await _uow.Queens.GetActiveByBeehiveIdsAsync(hiveIds);

            foreach (var hive in hives)
            {
                var hiveInspections = byHive.TryGetValue(hive.Id, out var list) ? list : [];

                if (staleEnabled) await ApplyStaleInspectionAsync(hive, apiary, hiveInspections, now, staleDays);
                if (dropEnabled)  await ApplyHoneyDropAsync(hive, apiary, hiveInspections);
                if (queenEnabled) await ApplyOldQueenAsync(hive, apiary, activeQueens, now);
            }

            if (stripsEnabled || karencaEnabled)
                await ApplyTreatmentRulesAsync(apiary, now, stripDays, stripsEnabled, karencaEnabled);

            if (roundOverdueEnabled)
                await ApplyTreatmentRoundOverdueAsync(apiary, now, roundOverdueDays);

            if (feedingEnabled)
                await ApplyFeedingRulesAsync(apiary, now, feedingDays);

            if (frostEnabled) await ApplyFrostAsync(apiary);
        }
    }

    // ── Rule 1: stale inspection ─────────────────────────────────────────────────

    private async Task ApplyStaleInspectionAsync(Beehive hive, Apiary apiary, List<Inspection> inspections, DateTime now, int staleDays)
    {
        // Measure from the last inspection, or the hive's creation when it has never been inspected
        // (a freshly-created hive is not "stale").
        var lastActivity = inspections.Count > 0 ? inspections[0].Date : hive.CreatedAt;
        var days = (int)(now - lastActivity).TotalDays;
        if (days < staleDays) return;

        var recipients = await HiveRecipientsAsync(hive, apiary);
        await DispatchAsync(recipients,
            "Košnica bez pregleda",
            $"Košnica '{hive.Name}' nije pregledana {days} dana.",
            NotificationType.InspectionOverdue, hive.Id, nameof(Beehive), TimeSpan.FromDays(7));
    }

    // ── Rule 2: honey level dropping ─────────────────────────────────────────────

    private async Task ApplyHoneyDropAsync(Beehive hive, Apiary apiary, List<Inspection> inspections)
    {
        if (inspections.Count < 2) return;

        var latest = inspections[0];
        var previous = inspections[1];
        var dropping = (int)latest.HoneyLevel < (int)previous.HoneyLevel && latest.HoneyLevel == HoneyLevel.Low;
        if (!dropping) return;

        var recipients = await HiveRecipientsAsync(hive, apiary);
        await DispatchAsync(recipients,
            "Opada nivo meda",
            $"Košnici '{hive.Name}' opada nivo meda — razmisli o prehrani.",
            NotificationType.HoneyLevelDrop, hive.Id, nameof(Beehive), TimeSpan.FromDays(7));
    }

    // ── Rule 4: old queen (SPEC-03) — evaluated only in the March scan month ──────

    private async Task ApplyOldQueenAsync(Beehive hive, Apiary apiary, Dictionary<int, Queen> activeQueens, DateTime now)
    {
        if (now.Month != 3) return;
        if (!activeQueens.TryGetValue(hive.Id, out var queen)) return;

        var season = now.Year - queen.Year + 1;
        if (season < 3) return;

        var recipients = await HiveRecipientsAsync(hive, apiary);
        await DispatchAsync(recipients,
            "Stara matica",
            $"Matica u košnici '{hive.Name}' je u {season}. sezoni — planiraj zamjenu.",
            NotificationType.OldQueen, hive.Id, nameof(Beehive), TimeSpan.FromDays(300));
    }

    // ── Rules 5+6: treatment register (SPEC-08) — strips left in + karenca ended ──

    private async Task ApplyTreatmentRulesAsync(Apiary apiary, DateTime now, int stripRemovalDays, bool stripsEnabled, bool karencaEnabled)
    {
        var treatments = (await _uow.Treatments.GetByApiaryAsync(apiary.Id, null)).ToList();
        if (treatments.Count == 0) return;

        var recipients = await ApiaryRecipientsAsync(apiary);

        foreach (var t in treatments)
        {
            if (stripsEnabled && t.Method == ApplicationMethod.Strips && t.EndDate is null)
            {
                var days = (int)(now - t.StartDate).TotalDays;
                if (days >= stripRemovalDays)
                    await DispatchAsync(recipients,
                        "Trake za uklanjanje",
                        $"Trake u košnicama pčelinjaka '{apiary.Name}' su unutra {days} dana — vrijeme je za uklanjanje.",
                        NotificationType.StripsLeftIn, t.Id, nameof(Treatment), TimeSpan.FromDays(7));
            }

            if (karencaEnabled && t.EndDate is not null && t.WithdrawalDays > 0)
            {
                var karencaUntil = TreatmentStatusHelper.KarencaUntil(t.StartDate, t.EndDate, t.WithdrawalDays);
                // Fire once shortly after expiry; a few days of slack covers missed scans, dedup guards repeats.
                if (karencaUntil <= now && karencaUntil >= now.AddDays(-3))
                    await DispatchAsync(recipients,
                        "Istekla karenca",
                        $"Istekla karenca za pčelinjak '{apiary.Name}' — med se ponovo smije vrcati.",
                        NotificationType.KarencaEnded, t.Id, nameof(Treatment), TimeSpan.FromDays(7));
            }
        }
    }

    // ── Rule: treatment application round overdue (apiary-level) — parity with feeding overdue ──

    private async Task ApplyTreatmentRoundOverdueAsync(Apiary apiary, DateTime now, int overdueDays)
    {
        var treatments = (await _uow.Treatments.GetByApiaryAsync(apiary.Id, null)).ToList();
        if (treatments.Count == 0) return;

        var recipients = await ApiaryRecipientsAsync(apiary);
        var threshold = now.AddDays(-overdueDays).Date;

        foreach (var t in treatments)
        {
            // Fire once per TREATMENT, not per round: a protocol several rounds behind should
            // produce one nudge, not several. The earliest overdue round decides whether it fires.
            var earliestOverdue = t.Rounds
                .Where(r => r.Status == TreatmentRoundStatus.Pending && r.ScheduledDate.Date <= threshold)
                .OrderBy(r => r.ScheduledDate)
                .FirstOrDefault();
            if (earliestOverdue is null) continue;

            await DispatchAsync(recipients,
                "Primjena tretmana kasni",
                $"Primjena tretmana kasni — pčelinjak '{apiary.Name}': runda zakazana za {earliestOverdue.ScheduledDate:dd.MM.yyyy.} još nije označena.",
                NotificationType.TreatmentRoundOverdue, t.Id, nameof(Treatment), TimeSpan.FromDays(3));
        }
    }

    // ── Rule 8: feeding overdue (apiary-level) — SPEC-12 Phase D ─────────────────

    private async Task ApplyFeedingRulesAsync(Apiary apiary, DateTime now, int overdueDays)
    {
        var diets = (await _uow.Diets.GetByApiaryAsync(apiary.Id))
            .Where(d => d.Status == DietStatus.InProgress)
            .ToList();
        if (diets.Count == 0) return;

        var recipients = await ApiaryRecipientsAsync(apiary);
        var threshold = now.AddDays(-overdueDays).Date;

        foreach (var d in diets)
        {
            // No hives on the programme → nothing to do in the field; same rule the calendar uses.
            if (!d.Beehives.Any(db => db.RemovedOn == null)) continue;

            // Fire once per DIET, not per round: a programme two weeks behind should produce one
            // nudge, not seven. The earliest overdue round decides whether it fires at all.
            var earliestOverdue = d.FeedingEntries
                .Where(e => e.Status == FeedingEntryStatus.Pending && e.ScheduledDate.Date <= threshold)
                .OrderBy(e => e.ScheduledDate)
                .FirstOrDefault();
            if (earliestOverdue is null) continue;

            await DispatchAsync(recipients,
                "Hranjenje kasni",
                $"Hranjenje kasni — pčelinjak '{apiary.Name}': runda zakazana za {earliestOverdue.ScheduledDate:dd.MM.yyyy.} još nije označena.",
                NotificationType.FeedingOverdue, d.Id, nameof(Diet), TimeSpan.FromDays(3));
        }
    }

    // ── Rule 3: frost warning (apiary-level) ─────────────────────────────────────

    private async Task ApplyFrostAsync(Apiary apiary)
    {
        if (apiary.Latitude is not double lat || apiary.Longitude is not double lon)
            return; // no coordinates → skip silently

        double minTemp;
        try
        {
            var forecast = await _weather.GetForecastAsync(lat, lon);
            // Next 48 h ≈ today + tomorrow.
            var upcoming = forecast.Daily.Take(2).Select(d => d.MinTemp).Where(t => t.HasValue).Select(t => t!.Value).ToList();
            if (upcoming.Count == 0) return;
            minTemp = upcoming.Min();
        }
        catch
        {
            // Weather API unreachable → skip frost this scan; other rules are unaffected.
            return;
        }

        if (minTemp >= 0) return;

        var recipients = await ApiaryRecipientsAsync(apiary);
        await DispatchAsync(recipients,
            "Najavljen mraz",
            $"Najavljen mraz za pčelinjak '{apiary.Name}' ({minTemp:0.#} °C). Provjeri prehranu i utopljenost.",
            NotificationType.FrostWarning, apiary.Id, nameof(Apiary), TimeSpan.FromDays(3));
    }

    // ── Rule 7: plan expiring (SPEC-09) — org-level, OrgAdmins only ──────────────

    private async Task ApplyPlanExpiringAsync(DateTime now)
    {
        var orgs = await _uow.Organizations.GetAllAsync();
        foreach (var org in orgs)
        {
            if (org.PlanValidUntil is not DateTime validUntil) continue;

            // Already expired (effective plan fell to Free) → nothing left to preserve.
            if (PlanHelper.Effective(org.Plan, org.PlanValidUntil, now) == PlanType.Free) continue;

            if ((validUntil.Date - now.Date).TotalDays > 7) continue;

            var recipients = await _uow.Users.GetOrganizationAdminIdsAsync(org.Id);
            await DispatchAsync(recipients,
                "Paket ističe",
                $"Vaš {Common.Localization.BsLabels.Label(org.Plan)} paket ističe {validUntil:dd.MM.yyyy.} — produžite da zadržite AI funkcije i limite.",
                NotificationType.PlanExpiring, org.Id, nameof(Organization), TimeSpan.FromDays(7));
        }
    }

    // ── Recipients ───────────────────────────────────────────────────────────────

    private async Task<HashSet<int>> HiveRecipientsAsync(Beehive hive, Apiary apiary)
    {
        var ids = new HashSet<int>();
        ids.UnionWith(await _uow.Users.GetUserIdsAssignedToBeehiveAsync(hive.Id));
        ids.UnionWith(await _uow.Users.GetApiaryAdminIdsAsync(apiary.Id));
        ids.UnionWith(await _uow.Users.GetOrganizationAdminIdsAsync(apiary.OrganizationId));
        return ids;
    }

    private async Task<HashSet<int>> ApiaryRecipientsAsync(Apiary apiary)
    {
        var ids = new HashSet<int>();
        ids.UnionWith(await _uow.Users.GetUserIdsAssignedToApiaryAsync(apiary.Id));
        ids.UnionWith(await _uow.Users.GetApiaryAdminIdsAsync(apiary.Id));
        ids.UnionWith(await _uow.Users.GetOrganizationAdminIdsAsync(apiary.OrganizationId));
        return ids;
    }

    // ── Dispatch with per-recipient dedup ────────────────────────────────────────

    private async Task DispatchAsync(
        IEnumerable<int> userIds, string title, string message,
        NotificationType type, int relatedEntityId, string relatedEntityType, TimeSpan dedupWindow)
    {
        var since = DateTime.UtcNow - dedupWindow;
        foreach (var userId in userIds.Distinct())
        {
            if (await _uow.Notifications.ExistsRecentAsync(userId, type, relatedEntityId, since))
                continue;

            await _notifications.NotifyAsync(userId, title, message, type, relatedEntityId, relatedEntityType);
        }
    }

    // ── Config helpers (indexer + manual parse — no Configuration.Binder dependency) ──

    private int GetInt(string key, int fallback) => int.TryParse(_config[key], out var v) ? v : fallback;
    private bool GetBool(string key, bool fallback) => bool.TryParse(_config[key], out var v) ? v : fallback;
}
