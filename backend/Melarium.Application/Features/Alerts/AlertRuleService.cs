using Melarium.Application.Common.Email;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Seasons;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Weather;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Alerts;

/// <summary>
/// Rule-based proactive alerts (SPEC-04 Part A), fitted to the beekeeping year since SPEC-29: whether a
/// rule runs, its threshold, dedup window and priority all come from <see cref="INotificationPolicy"/>
/// for the phase the organization is in. Hive rules are grouped — one notification per apiary per
/// recipient, listing only the hives that recipient is responsible for — and every candidate is
/// deduplicated against the notifications table, so re-running the scan never produces duplicates.
/// </summary>
public class AlertRuleService : IAlertRuleService
{
    /// <summary>Hives named in one grouped message; the rest are counted, not listed.</summary>
    private const int MaxListedHives = 15;

    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly IWeatherService _weather;
    private readonly IConfiguration _config;
    private readonly Common.Security.IPlanLock _planLock;
    private readonly ISeasonCalendar _seasons;
    private readonly INotificationPolicy _policy;
    private readonly TimeProvider _time;

    public AlertRuleService(
        IUnitOfWork uow,
        INotificationService notifications,
        IWeatherService weather,
        IConfiguration config,
        Common.Security.IPlanLock planLock,
        ISeasonCalendar seasons,
        INotificationPolicy policy,
        TimeProvider time)
    {
        _uow = uow;
        _notifications = notifications;
        _weather = weather;
        _config = config;
        _planLock = planLock;
        _seasons = seasons;
        _policy = policy;
        _time = time;
    }

    public async Task RunDailyScanAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var today = _seasons.LocalDate(now);

        var stripDays        = GetInt("Alerts:StripRemovalDays", 42);
        var feedingDays      = GetInt("Alerts:FeedingOverdueDays", 2);
        var roundOverdueDays = GetInt("Alerts:TreatmentRoundOverdueDays", 2);

        var orgs = (await _uow.Organizations.GetAllAsync()).ToList();
        var byOrg = orgs.ToDictionary(o => o.Id, o => (Org: o, Season: _seasons.For(today, o.SeasonShiftDays)));

        await ApplyPlanExpiringAsync(byOrg.Values, now);
        await ApplyPlanLockPendingAsync(byOrg.Values, now);
        await ApplySeasonPhaseStartedAsync(byOrg.Values, today, now);

        var apiaries = (await _uow.Apiaries.GetAllAsync()).ToList();

        foreach (var apiary in apiaries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!byOrg.TryGetValue(apiary.OrganizationId, out var owner)) continue;
            var season = owner.Season;

            // Never alert about something the recipient cannot open (SPEC-24). "Pregled kasni za
            // košnicu X" that leads to a paywall is worse than silence, and it would also leak the
            // state of a hive the plan has locked away.
            var locked = await _planLock.GetForOrganizationAsync(apiary.OrganizationId);
            if (locked.ApiaryIds.Contains(apiary.Id)) continue;

            var hives = (await _uow.Beehives.GetByApiaryIdAsync(apiary.Id))
                .Where(h => !locked.BeehiveIds.Contains(h.Id))
                .ToList();
            if (hives.Count == 0)
            {
                await ApplyFrostAsync(apiary, season, now);
                continue;
            }

            var hiveIds = hives.Select(h => h.Id).ToList();

            var inspections = (await _uow.Inspections.FindAsync(i => hiveIds.Contains(i.BeehiveId))).ToList();
            var byHive = inspections
                .GroupBy(i => i.BeehiveId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.Date).ToList());

            var activeQueens = await _uow.Queens.GetActiveByBeehiveIdsAsync(hiveIds);

            await ApplyHiveRulesAsync(apiary, hives, byHive, activeQueens, owner.Org.SeasonShiftDays, season, today, now);
            await ApplyTreatmentRulesAsync(apiary, season, now, stripDays);
            await ApplyTreatmentRoundOverdueAsync(apiary, season, now, roundOverdueDays);
            await ApplyFeedingRulesAsync(apiary, season, now, feedingDays);
            await ApplyFrostAsync(apiary, season, now);
        }
    }

    // ── Hive rules, grouped per apiary: inspection overdue, honey level, old queen ─────────

    private async Task ApplyHiveRulesAsync(
        Apiary apiary, List<Beehive> hives, Dictionary<int, List<Inspection>> byHive,
        Dictionary<int, Queen> activeQueens, int shiftDays, SeasonInfo season, DateOnly today, DateTime now)
    {
        var overdueRule = _policy.For(NotificationType.InspectionOverdue, season.Phase);
        var dropRule    = _policy.For(NotificationType.HoneyLevelDrop, season.Phase);
        var queenRule   = _policy.For(NotificationType.OldQueen, season.Phase);

        var overdue  = new List<HiveFinding>();
        var dropping = new List<HiveFinding>();
        var oldQueen = new List<HiveFinding>();

        foreach (var hive in hives)
        {
            var inspections = byHive.TryGetValue(hive.Id, out var list) ? list : [];

            if (overdueRule.Enabled)
            {
                // Measured from the last inspection, or the hive's creation when it has never been
                // inspected (a freshly-created hive is not "stale").
                var lastDay = _seasons.LocalDate(inspections.Count > 0 ? inspections[0].Date : hive.CreatedAt);
                if (_policy.IsInspectionOverdue(lastDay, today, shiftDays))
                {
                    var detail = inspections.Count > 0
                        ? BsLabels.Count(today.DayNumber - lastDay.DayNumber, "dan", "dana", "dana")
                        : "još nije pregledana";
                    overdue.Add(new(hive, detail));
                }
            }

            if (dropRule.Enabled && inspections.Count >= 2
                && AlertConditions.IsHoneyDropping(inspections[0].HoneyLevel, inspections[1].HoneyLevel))
                dropping.Add(new(hive, null));

            if (queenRule.Enabled && activeQueens.TryGetValue(hive.Id, out var queen))
            {
                var seasonNo = today.Year - queen.Year + 1;
                if (seasonNo >= 3) oldQueen.Add(new(hive, $"{seasonNo}. sezona", seasonNo));
            }
        }

        await DispatchGroupedAsync(apiary, overdue, NotificationType.InspectionOverdue, overdueRule, season, now,
            one => ("Košnica bez pregleda", one.Detail == "još nije pregledana"
                ? $"Košnica '{one.Hive.Name}' (pčelinjak '{apiary.Name}') još nije pregledana."
                : $"Košnica '{one.Hive.Name}' (pčelinjak '{apiary.Name}') nije pregledana {one.Detail}."),
            many => ("Košnice bez pregleda",
                $"Pčelinjak '{apiary.Name}' ({BsLabels.Count(many.Count, "košnica", "košnice", "košnica")}):"));

        await DispatchGroupedAsync(apiary, dropping, NotificationType.HoneyLevelDrop, dropRule, season, now,
            one => ("Opada nivo meda",
                $"Košnici '{one.Hive.Name}' (pčelinjak '{apiary.Name}') opada nivo meda — razmisli o prehrani."),
            many => ("Opada nivo meda",
                $"Pčelinjak '{apiary.Name}' ({BsLabels.Count(many.Count, "košnica", "košnice", "košnica")}) — razmisli o prehrani:"));

        await DispatchGroupedAsync(apiary, oldQueen, NotificationType.OldQueen, queenRule, season, now,
            one => ("Stara matica",
                $"Matica u košnici '{one.Hive.Name}' (pčelinjak '{apiary.Name}') je u {one.Number}. sezoni — planiraj zamjenu."),
            many => ("Stare matice",
                $"Pčelinjak '{apiary.Name}' — matice u 3. sezoni ili starije, planiraj zamjenu:"));
    }

    /// <summary>
    /// One notification per recipient per apiary, naming only the hives that recipient answers for:
    /// apiary and organization admins get every hive in the finding, an assigned beekeeper only theirs.
    /// Keyed on the apiary, so a hive that becomes overdue after the reminder went out is picked up by
    /// the next one instead of producing a second message the same week.
    /// </summary>
    private async Task DispatchGroupedAsync(
        Apiary apiary, List<HiveFinding> findings, NotificationType type, AlertRule rule, SeasonInfo season, DateTime now,
        Func<HiveFinding, (string Title, string Message)> single,
        Func<IReadOnlyList<HiveFinding>, (string Title, string Header)> multiple)
    {
        if (findings.Count == 0) return;

        var managers = new HashSet<int>();
        managers.UnionWith(await _uow.Users.GetApiaryAdminIdsAsync(apiary.Id));
        managers.UnionWith(await _uow.Users.GetOrganizationAdminIdsAsync(apiary.OrganizationId));

        var byUser = managers.ToDictionary(id => id, _ => new List<HiveFinding>(findings));
        foreach (var finding in findings)
        {
            foreach (var userId in await _uow.Users.GetUserIdsAssignedToBeehiveAsync(finding.Hive.Id))
            {
                if (managers.Contains(userId)) continue;
                if (!byUser.TryGetValue(userId, out var own)) byUser[userId] = own = [];
                own.Add(finding);
            }
        }

        var since = DedupSince(rule, season, now);
        foreach (var (userId, own) in byUser)
        {
            if (own.Count == 0) continue;
            if (await _uow.Notifications.ExistsRecentAsync(userId, type, apiary.Id, since)) continue;

            string title, message;
            if (own.Count == 1)
            {
                (title, message) = single(own[0]);
            }
            else
            {
                (title, var header) = multiple(own);
                var lines = own.Take(MaxListedHives)
                    .Select(f => f.Detail is null ? $"- {f.Hive.Name}" : $"- {f.Hive.Name} ({f.Detail})");
                message = header + "\n" + string.Join("\n", lines);
                if (own.Count > MaxListedHives) message += $"\n- … i još {own.Count - MaxListedHives}";
            }

            await _notifications.NotifyAsync(userId, title, message, type, apiary.Id, nameof(Apiary), rule.Priority);
        }
    }

    // ── Treatment register (SPEC-08) — strips left in + karenca ended ─────────────────────
    // Tied to work the beekeeper started, so the season never silences them.

    private async Task ApplyTreatmentRulesAsync(Apiary apiary, SeasonInfo season, DateTime now, int stripRemovalDays)
    {
        var stripsRule  = _policy.For(NotificationType.StripsLeftIn, season.Phase);
        var karencaRule = _policy.For(NotificationType.KarencaEnded, season.Phase);
        if (!stripsRule.Enabled && !karencaRule.Enabled) return;

        var treatments = (await _uow.Treatments.GetByApiaryAsync(apiary.Id, null)).ToList();
        if (treatments.Count == 0) return;

        var recipients = await ApiaryRecipientsAsync(apiary);

        foreach (var t in treatments)
        {
            if (stripsRule.Enabled && AlertConditions.StripsOverdueDays(t, now, stripRemovalDays) is int days)
                await DispatchAsync(recipients,
                    "Trake za uklanjanje",
                    $"Trake u košnicama pčelinjaka '{apiary.Name}' su unutra {days} dana — vrijeme je za uklanjanje.",
                    NotificationType.StripsLeftIn, t.Id, nameof(Treatment), stripsRule, season, now);

            if (karencaRule.Enabled && t.EndDate is not null && t.WithdrawalDays > 0)
            {
                var karencaUntil = TreatmentStatusHelper.KarencaUntil(t.StartDate, t.EndDate, t.WithdrawalDays);
                // Fire once shortly after expiry; a few days of slack covers missed scans, dedup guards repeats.
                if (karencaUntil <= now && karencaUntil >= now.AddDays(-3))
                    await DispatchAsync(recipients,
                        "Istekla karenca",
                        $"Istekla karenca za pčelinjak '{apiary.Name}' — med se ponovo smije vrcati.",
                        NotificationType.KarencaEnded, t.Id, nameof(Treatment), karencaRule, season, now);
            }
        }
    }

    // ── Treatment application round overdue (apiary-level) — parity with feeding overdue ──

    private async Task ApplyTreatmentRoundOverdueAsync(Apiary apiary, SeasonInfo season, DateTime now, int overdueDays)
    {
        var rule = _policy.For(NotificationType.TreatmentRoundOverdue, season.Phase);
        if (!rule.Enabled) return;

        var treatments = (await _uow.Treatments.GetByApiaryAsync(apiary.Id, null)).ToList();
        if (treatments.Count == 0) return;

        var recipients = await ApiaryRecipientsAsync(apiary);

        foreach (var t in treatments)
        {
            if (AlertConditions.EarliestOverdueRound(t, now, overdueDays) is not { } earliestOverdue) continue;

            await DispatchAsync(recipients,
                "Primjena tretmana kasni",
                $"Pčelinjak '{apiary.Name}': runda zakazana za {earliestOverdue.ScheduledDate:dd.MM.yyyy.} još nije označena.",
                NotificationType.TreatmentRoundOverdue, t.Id, nameof(Treatment), rule, season, now);
        }
    }

    // ── Feeding overdue (apiary-level) — SPEC-12 Phase D ──────────────────────────────────

    private async Task ApplyFeedingRulesAsync(Apiary apiary, SeasonInfo season, DateTime now, int overdueDays)
    {
        var rule = _policy.For(NotificationType.FeedingOverdue, season.Phase);
        if (!rule.Enabled) return;

        var diets = (await _uow.Diets.GetByApiaryAsync(apiary.Id))
            .Where(d => d.Status == DietStatus.InProgress)
            .ToList();
        if (diets.Count == 0) return;

        var recipients = await ApiaryRecipientsAsync(apiary);

        foreach (var d in diets)
        {
            // Once per DIET, not per round, and never for a programme with no hive left on it.
            if (AlertConditions.EarliestOverdueFeeding(d, now, overdueDays) is not { } earliestOverdue) continue;

            await DispatchAsync(recipients,
                "Hranjenje kasni",
                $"Pčelinjak '{apiary.Name}': runda zakazana za {earliestOverdue.ScheduledDate:dd.MM.yyyy.} još nije označena.",
                NotificationType.FeedingOverdue, d.Id, nameof(Diet), rule, season, now);
        }
    }

    // ── Frost (apiary-level) — what counts as news depends on the phase ───────────────────

    private async Task ApplyFrostAsync(Apiary apiary, SeasonInfo season, DateTime now)
    {
        var rule = _policy.For(NotificationType.FrostWarning, season.Phase);
        if (!rule.Enabled || rule.BelowCelsius is not double limit) return;

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

        if (minTemp >= limit) return;

        var (title, message, advice) = season.Phase switch
        {
            SeasonPhase.Winter => ("Ekstremna hladnoća",
                $"Najavljena ekstremna hladnoća za pčelinjak '{apiary.Name}' ({minTemp:0.#} °C). Provjeri utopljenost i da leto nije zatrpano.",
                "Provjeri utopljenost i da leto nije zatrpano."),
            SeasonPhase.Wintering => ("Prvi mraz",
                $"Najavljen prvi mraz za pčelinjak '{apiary.Name}' ({minTemp:0.#} °C) — vrijeme je da se završi zazimljavanje.",
                "Vrijeme je da se završi zazimljavanje."),
            _ => ("Najavljen mraz",
                $"Najavljen mraz za pčelinjak '{apiary.Name}' ({minTemp:0.#} °C). Provjeri prehranu i utopljenost.",
                "Provjeri prehranu i utopljenost."),
        };

        var recipients = await ApiaryRecipientsAsync(apiary);
        await DispatchAsync(recipients, title, message,
            NotificationType.FrostWarning, apiary.Id, nameof(Apiary), rule, season, now,
            AlertEmails.Frost(title, apiary, minTemp, advice));
    }

    // ── Season phase started (SPEC-29) — once per phase per member ────────────────────────

    private async Task ApplySeasonPhaseStartedAsync(
        IEnumerable<(Organization Org, SeasonInfo Season)> orgs, DateOnly today, DateTime now)
    {
        List<LearningTopic>? topics = null;

        foreach (var (org, season) in orgs)
        {
            var rule = _policy.For(NotificationType.SeasonPhaseStarted, season.Phase);
            if (!rule.Enabled) continue;

            // Only near the start of a phase. A deploy in late September, or a member who joins in the
            // middle of summer, must not be told weeks late that summer "has begun".
            if (today.DayNumber - season.Start.DayNumber >= _policy.PhaseNoticeDays) continue;

            var members = await _uow.Users.GetIdsByOrganizationAsync(org.Id);
            if (members.Count == 0) continue;

            topics ??= (await _uow.LearningTopics.GetPublishedAsync(LearningCategory.SezonskiRadovi)).ToList();
            var (title, message) = ComposePhaseNotice(season, topics);

            // One key per phase and year; the window is wider than any phase, so an admin moving the
            // shift mid-phase cannot make the same notice go out twice.
            var key = season.Start.Year * 10 + (int)season.Phase;
            var since = now.AddDays(-200);

            foreach (var userId in members)
            {
                if (await _uow.Notifications.ExistsRecentAsync(userId, NotificationType.SeasonPhaseStarted, key, since))
                    continue;
                await _notifications.NotifyAsync(userId, title, message,
                    NotificationType.SeasonPhaseStarted, key, "SeasonPhase", rule.Priority);
            }
        }
    }

    private static (string Title, string Message) ComposePhaseNotice(SeasonInfo season, List<LearningTopic> topics)
    {
        var label = BsLabels.Label(season.Phase);
        var lines = new List<string>
        {
            // The dates end in their own period, so the sentence runs on with a dash.
            $"Traje od {season.Start:dd.MM.} do {season.End:dd.MM.} — radovi za ovu fazu:",
        };
        lines.AddRange(SeasonalTasks.For(season.Phase).Select(t => $"- {t}"));

        var months = PhaseMonths(season);
        var related = topics
            .Where(t => t.Months is { Length: > 0 } m && m.Any(months.Contains))
            .OrderByDescending(t => t.PublishedAt)
            .Take(3)
            .ToList();
        if (related.Count > 0)
        {
            lines.Add("");
            lines.Add("Iz Edukacije:");
            lines.AddRange(related.Select(t => $"- {t.Title}"));
        }

        return ($"Počinje {char.ToLowerInvariant(label[0])}{label[1..]}", string.Join("\n", lines));
    }

    private static HashSet<int> PhaseMonths(SeasonInfo season)
    {
        var months = new HashSet<int>();
        for (var d = new DateOnly(season.Start.Year, season.Start.Month, 1); d <= season.End; d = d.AddMonths(1))
            months.Add(d.Month);
        return months;
    }

    // ── Plan expiring (SPEC-09) — org-level, OrgAdmins only; never season-dependent ──────

    private async Task ApplyPlanExpiringAsync(IEnumerable<(Organization Org, SeasonInfo Season)> orgs, DateTime now)
    {
        foreach (var (org, season) in orgs)
        {
            var rule = _policy.For(NotificationType.PlanExpiring, season.Phase);
            if (!rule.Enabled) return;
            if (org.PlanValidUntil is not DateTime validUntil) continue;

            // Already expired (effective plan fell to Free) → nothing left to preserve.
            if (PlanHelper.Effective(org.Plan, org.PlanValidUntil, now) == PlanType.Free) continue;

            if ((validUntil.Date - now.Date).TotalDays > 7) continue;

            var recipients = await _uow.Users.GetOrganizationAdminIdsAsync(org.Id);
            await DispatchAsync(recipients,
                "Paket ističe",
                $"Vaš {BsLabels.Label(org.Plan)} paket ističe {validUntil:dd.MM.yyyy.} — produžite da zadržite AI funkcije i limite.",
                NotificationType.PlanExpiring, org.Id, nameof(Organization), rule, season, now);
        }
    }

    // ── Data about to be locked (SPEC-24) — org-level, OrgAdmins only ─────────────────────

    /// <summary>
    /// Two days before a plan expires, tell the organizations that will actually lose reach of their
    /// own data exactly what stops opening. Deliberately separate from <see cref="ApplyPlanExpiringAsync"/>:
    /// that one goes to everyone whose plan is ending, while this one stays silent for an organization
    /// that fits inside the free limits and would notice nothing.
    ///
    /// This only covers expiry, which is how almost every downgrade happens — the registration trial
    /// running out, or a paid year ending. A SystemAdmin moving an organization down by hand takes
    /// effect immediately and is announced by the person who did it.
    /// </summary>
    private async Task ApplyPlanLockPendingAsync(IEnumerable<(Organization Org, SeasonInfo Season)> orgs, DateTime now)
    {
        var noticeDays = GetInt("Alerts:PlanLockNoticeDays", 2);

        foreach (var (org, season) in orgs)
        {
            var rule = _policy.For(NotificationType.PlanLockPending, season.Phase);
            if (!rule.Enabled) return;
            if (org.PlanValidUntil is not DateTime validUntil) continue;

            // Already expired — the lock is on, and this warning is about something still to come.
            if (PlanHelper.Effective(org.Plan, org.PlanValidUntil, now) == PlanType.Free) continue;

            var daysLeft = (validUntil.Date - now.Date).TotalDays;
            if (daysLeft > noticeDays) continue;

            // What the org will look like the morning after: expiry always lands on Free.
            var pending = await _planLock.PreviewForPlanAsync(org.Id, PlanType.Free);
            if (pending.IsEmpty) continue;

            var recipients = await _uow.Users.GetOrganizationAdminIdsAsync(org.Id);
            var plan = BsLabels.Label(org.Plan);
            await DispatchAsync(recipients,
                "Dio podataka postaje nedostupan",
                $"Vaš {plan} paket ističe {validUntil:dd.MM.yyyy.} " +
                $"Nakon toga {LockSummary(pending)} ostaje nedostupno dok ne produžite paket — podaci se ne brišu.",
                NotificationType.PlanLockPending, org.Id, nameof(Organization), rule, season, now,
                AlertEmails.PlanLock(plan, validUntil, LockSummary(pending)));
        }
    }

    /// <summary>"3 pčelinjaka i 43 košnice" — only the parts that are actually non-zero.</summary>
    private static string LockSummary(PlanLockResult pending)
    {
        var parts = new List<string>();
        if (pending.ApiaryIds.Count > 0) parts.Add(BsLabels.Count(pending.ApiaryIds.Count, "pčelinjak", "pčelinjaka", "pčelinjaka"));
        if (pending.BeehiveIds.Count > 0) parts.Add(BsLabels.Count(pending.BeehiveIds.Count, "košnica", "košnice", "košnica"));
        return string.Join(" i ", parts);
    }

    // ── Recipients ───────────────────────────────────────────────────────────────

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
        NotificationType type, int relatedEntityId, string relatedEntityType,
        AlertRule rule, SeasonInfo season, DateTime now, EmailContent? email = null)
    {
        var since = DedupSince(rule, season, now);
        foreach (var userId in userIds.Distinct())
        {
            if (await _uow.Notifications.ExistsRecentAsync(userId, type, relatedEntityId, since))
                continue;

            await _notifications.NotifyAsync(userId, title, message, type, relatedEntityId, relatedEntityType, rule.Priority, email);
        }
    }

    private DateTime DedupSince(AlertRule rule, SeasonInfo season, DateTime now) =>
        rule.OncePerPhase ? _seasons.StartOfDayUtc(season.Start) : now - rule.DedupWindow;

    // ── Config helpers (indexer + manual parse — no Configuration.Binder dependency) ──

    private int GetInt(string key, int fallback) => int.TryParse(_config[key], out var v) ? v : fallback;

    private sealed record HiveFinding(Beehive Hive, string? Detail, int Number = 0);
}
