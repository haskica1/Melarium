using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Seasons;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Alerts;
using Melarium.Application.Features.Calendar;
using Melarium.Application.Features.Dashboard.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Todos;
using Melarium.Application.Features.Weather;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Dashboard;

/// <summary>
/// The start page (SPEC-29). Scope comes from <see cref="IAccessGuard"/>'s accessible sets, which are
/// already role-scoped and already drop what the plan has locked — the ADR-043 pattern, so this is not
/// an eighth hand-filtered aggregate. "Traži pažnju" is evaluated live by the same
/// <see cref="INotificationPolicy"/> and <see cref="AlertConditions"/> as the alert scan, so the page
/// and the notifications cannot disagree about what is overdue.
/// </summary>
public class DashboardService : IDashboardService
{
    private const int TodosShown = 5;
    private const int ObligationDays = 7;
    private const int ItemsShown = 10;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessGuard _access;
    private readonly IPlanLock _planLock;
    private readonly ISeasonCalendar _seasons;
    private readonly INotificationPolicy _policy;
    private readonly ICalendarObligationService _obligations;
    private readonly ITodoService _todos;
    private readonly IWeatherService _weather;
    private readonly IConfiguration _config;
    private readonly TimeProvider _time;

    public DashboardService(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IAccessGuard access,
        IPlanLock planLock,
        ISeasonCalendar seasons,
        INotificationPolicy policy,
        ICalendarObligationService obligations,
        ITodoService todos,
        IWeatherService weather,
        IConfiguration config,
        TimeProvider time)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _access      = access;
        _planLock    = planLock;
        _seasons     = seasons;
        _policy      = policy;
        _obligations = obligations;
        _todos       = todos;
        _weather     = weather;
        _config      = config;
        _time        = time;
    }

    public async Task<DashboardDto> GetAsync()
    {
        var org    = await LoadOrganizationAsync();
        var now    = _time.GetUtcNow().UtcDateTime;
        var today  = _seasons.LocalDate(now);
        var season = _seasons.For(today, org.SeasonShiftDays);

        var apiaries    = await _access.GetAccessibleApiariesAsync();
        var hives       = await _access.GetAccessibleBeehivesAsync();
        var apiaryNames = apiaries.ToDictionary(a => a.Id, a => a.Name);
        var hiveIds     = hives.Select(h => h.Id).ToList();
        var hiveSet     = hiveIds.ToHashSet();
        var apiaryIds   = apiaries.Select(a => a.Id).ToList();
        var isBeekeeper = _currentUser.Role == UserRole.Beekeeper;

        var lastDates = await _uow.Inspections.GetLastDatesAsync(hiveIds);
        var levels    = await _uow.Inspections.GetLevelsSinceAsync(hiveIds, now.AddDays(-400));

        // A Beekeeper's apiaries are the ones *containing* their hives; narrow the apiary-level work to
        // what touches those hives, the same way the calendar does.
        var treatments = (await _uow.Treatments.GetByApiaryIdsAsync(apiaryIds))
            .Where(t => !isBeekeeper || t.Entries.Any(e => hiveSet.Contains(e.BeehiveId)))
            .ToList();
        var diets = (await _uow.Diets.GetByApiaryIdsAsync(apiaryIds))
            .Where(d => !isBeekeeper || d.Beehives.Any(db => db.RemovedOn == null && hiveSet.Contains(db.BeehiveId)))
            .ToList();

        var hiveStatus = new List<HiveStatusDto>();
        var attention  = new List<AttentionItemDto>();

        CollectInspectionState(season, org.SeasonShiftDays, today, hives, apiaryNames, lastDates, hiveStatus, attention);
        CollectHoneyDrop(season, hives, apiaryNames, levels, attention);
        CollectWorkInProgress(season, now, apiaryNames, treatments, diets, attention);

        var openTodos = (await _todos.GetAllOpenForCurrentUserAsync()).ToList();
        var (yieldByMonth, yieldThisYear, yieldLastYearToDate) = await YieldAsync(apiaryIds, hiveSet, isBeekeeper, today);

        return new DashboardDto
        {
            Season = new DashboardSeasonDto
            {
                Phase     = season.Phase,
                Start     = season.Start,
                End       = season.End,
                NextPhase = season.NextPhase,
                NextStart = season.NextStart,
                ShiftDays = org.SeasonShiftDays,
                Phases    = _seasons.CycleFor(today, org.SeasonShiftDays),
            },
            Counts = new DashboardCountsDto
            {
                Apiaries              = apiaries.Count,
                Beehives              = hives.Count,
                InspectionsThisMonth  = levels.Count(l => SameMonth(_seasons.LocalDate(l.Date), today)),
                YieldThisYearKg       = yieldThisYear,
                YieldLastYearToDateKg = yieldLastYearToDate,
                OpenTodos             = openTodos.Count,
                OverdueTodos          = openTodos.Count(t => IsOverdue(t.DueDate, today)),
            },
            Attention = attention
                .OrderBy(a => a.Priority == nameof(NotificationPriority.Critical) ? 0 : 1)
                .ThenBy(a => a.ApiaryName, StringComparer.CurrentCulture)
                .ToList(),
            Obligations  = await ObligationsAsync(today, hives, apiaryNames),
            HivesResting = season.Phase == SeasonPhase.Winter,
            HiveStatus   = hiveStatus.OrderBy(s => s.ApiaryName, StringComparer.CurrentCulture).ToList(),
            Programmes   = Programmes(now, apiaryNames, treatments, diets),
            YieldByMonth = yieldByMonth,
            OpenTodos    = TopTodos(openTodos, today, hives, apiaryNames),
            Topic        = await TopicAsync(today),
        };
    }

    public async Task<IReadOnlyList<ApiaryWeatherDto>> GetWeatherAsync()
    {
        var org    = await LoadOrganizationAsync();
        var today  = _seasons.LocalDate(_time.GetUtcNow().UtcDateTime);
        var season = _seasons.For(today, org.SeasonShiftDays);
        var frost  = _policy.For(NotificationType.FrostWarning, season.Phase);

        var located = (await _access.GetAccessibleApiariesAsync())
            .Where(a => a.Latitude is not null && a.Longitude is not null)
            .OrderBy(a => a.Name, StringComparer.CurrentCulture)
            .ToList();

        var results = await Task.WhenAll(located.Select(async apiary =>
        {
            try
            {
                var forecast = await _weather.GetForecastAsync(apiary.Latitude!.Value, apiary.Longitude!.Value);
                var days = forecast.Daily.Take(3)
                    .Select(d => new WeatherDayDto(d.Date, d.MinTemp, d.MaxTemp, d.PrecipitationProbability, d.WeatherCode))
                    .ToList();

                // Same 48 hours and the same limit as the scan's frost rule for this phase.
                var mins = forecast.Daily.Take(2).Select(d => d.MinTemp).OfType<double>().ToList();
                var warn = frost is { Enabled: true, BelowCelsius: double limit } && mins.Count > 0 && mins.Min() < limit;

                return new ApiaryWeatherDto(apiary.Id, apiary.Name, days,
                    warn ? frost.Priority.ToString() : null,
                    warn ? mins.Min() : null);
            }
            catch
            {
                // Forecast unavailable for this apiary — the card shows the others.
                return null;
            }
        }));

        return results.OfType<ApiaryWeatherDto>().ToList();
    }

    // ── Traži pažnju + stanje košnica ────────────────────────────────────────────

    private void CollectInspectionState(
        SeasonInfo season, int shiftDays, DateOnly today, IReadOnlyList<Beehive> hives,
        Dictionary<int, string> apiaryNames, Dictionary<int, DateTime> lastDates,
        List<HiveStatusDto> hiveStatus, List<AttentionItemDto> attention)
    {
        var rule = _policy.For(NotificationType.InspectionOverdue, season.Phase);

        foreach (var group in hives.GroupBy(h => h.ApiaryId))
        {
            int inTime = 0, late = 0, never = 0;
            var overdue = new List<string>();

            foreach (var hive in group.OrderBy(h => h.Name, StringComparer.CurrentCulture))
            {
                var inspected = lastDates.TryGetValue(hive.Id, out var last);
                var lastDay = _seasons.LocalDate(inspected ? last : hive.CreatedAt);

                if (!_policy.IsInspectionOverdue(lastDay, today, shiftDays)) { inTime++; continue; }

                if (inspected) late++; else never++;
                overdue.Add(inspected
                    ? $"{hive.Name} · {BsLabels.Count(today.DayNumber - lastDay.DayNumber, "dan", "dana", "dana")}"
                    : $"{hive.Name} · još nije pregledana");
            }

            var name = apiaryNames.GetValueOrDefault(group.Key, string.Empty);
            hiveStatus.Add(new HiveStatusDto(group.Key, name, inTime, late, never));

            if (rule.Enabled && overdue.Count > 0)
                attention.Add(new AttentionItemDto(
                    nameof(NotificationType.InspectionOverdue), rule.Priority.ToString(), group.Key, name,
                    $"{BsLabels.Count(overdue.Count, "košnica", "košnice", "košnica")} bez pregleda",
                    Cap(overdue), $"/apiaries/{group.Key}"));
        }
    }

    private void CollectHoneyDrop(
        SeasonInfo season, IReadOnlyList<Beehive> hives, Dictionary<int, string> apiaryNames,
        List<InspectionLevelInfo> levels, List<AttentionItemDto> attention)
    {
        var rule = _policy.For(NotificationType.HoneyLevelDrop, season.Phase);
        if (!rule.Enabled) return;

        // Levels come newest first, so each hive's first two are its last two inspections.
        var dropping = levels
            .GroupBy(l => l.BeehiveId)
            .Select(g => g.Take(2).ToList())
            .Where(two => two.Count == 2 && AlertConditions.IsHoneyDropping(two[0].HoneyLevel, two[1].HoneyLevel))
            .Select(two => two[0].BeehiveId)
            .ToHashSet();

        foreach (var group in hives.Where(h => dropping.Contains(h.Id)).GroupBy(h => h.ApiaryId))
        {
            var names = group.Select(h => h.Name).OrderBy(n => n, StringComparer.CurrentCulture).ToList();
            attention.Add(new AttentionItemDto(
                nameof(NotificationType.HoneyLevelDrop), rule.Priority.ToString(), group.Key,
                apiaryNames.GetValueOrDefault(group.Key, string.Empty),
                $"Opada nivo meda u {BsLabels.Count(names.Count, "košnici", "košnice", "košnica")}",
                Cap(names), $"/apiaries/{group.Key}"));
        }
    }

    private void CollectWorkInProgress(
        SeasonInfo season, DateTime now, Dictionary<int, string> apiaryNames,
        List<Treatment> treatments, List<Diet> diets, List<AttentionItemDto> attention)
    {
        var stripsRule = _policy.For(NotificationType.StripsLeftIn, season.Phase);
        var roundsRule = _policy.For(NotificationType.TreatmentRoundOverdue, season.Phase);
        var feedRule   = _policy.For(NotificationType.FeedingOverdue, season.Phase);
        var stripDays  = GetInt("Alerts:StripRemovalDays", 42);
        var roundDays  = GetInt("Alerts:TreatmentRoundOverdueDays", 2);
        var feedDays   = GetInt("Alerts:FeedingOverdueDays", 2);

        foreach (var t in treatments)
        {
            var apiary = apiaryNames.GetValueOrDefault(t.ApiaryId, string.Empty);

            if (stripsRule.Enabled && AlertConditions.StripsOverdueDays(t, now, stripDays) is int days)
                attention.Add(new AttentionItemDto(
                    nameof(NotificationType.StripsLeftIn), stripsRule.Priority.ToString(), t.ApiaryId, apiary,
                    $"Trake unutra {days} dana — {t.ProductName}", [], $"/treatments/{t.Id}"));

            if (roundsRule.Enabled && AlertConditions.EarliestOverdueRound(t, now, roundDays) is { } round)
                attention.Add(new AttentionItemDto(
                    nameof(NotificationType.TreatmentRoundOverdue), roundsRule.Priority.ToString(), t.ApiaryId, apiary,
                    $"Tretman kasni — {t.ProductName}, runda od {round.ScheduledDate:dd.MM.}", [], $"/treatments/{t.Id}"));
        }

        if (!feedRule.Enabled) return;
        foreach (var d in diets)
        {
            if (AlertConditions.EarliestOverdueFeeding(d, now, feedDays) is not { } entry) continue;
            attention.Add(new AttentionItemDto(
                nameof(NotificationType.FeedingOverdue), feedRule.Priority.ToString(), d.ApiaryId,
                apiaryNames.GetValueOrDefault(d.ApiaryId, string.Empty),
                $"Hranjenje kasni — {d.Name}, runda od {entry.ScheduledDate:dd.MM.}", [], $"/feedings/{d.Id}"));
        }
    }

    // ── Obaveze ──────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<DashboardObligationDto>> ObligationsAsync(
        DateOnly today, IReadOnlyList<Beehive> hives, Dictionary<int, string> apiaryNames)
    {
        if (_currentUser.UserId is not int userId || _currentUser.Role is not UserRole role) return [];

        var ctx = new CalendarUserContext(userId, role, _currentUser.OrganizationId, _currentUser.ApiaryId);
        var items = await _obligations.GatherAsync(ctx, today, today.AddDays(ObligationDays), CalendarCategories.All);
        var hiveApiary = hives.ToDictionary(h => h.Id, h => h.ApiaryId);

        var result = items
            .Where(o => o.Kind != ObligationKind.InspectionDue)
            .Select(o => new DashboardObligationDto(o.Date, o.Kind.ToString(), o.Title, LinkFor(o.BeehiveId, o.ApiaryId)))
            .ToList();

        // Recommended inspections are one per hive in the calendar (stable ICS ids); on the page they
        // read better as one line per apiary and day.
        foreach (var group in items
                     .Where(o => o.Kind == ObligationKind.InspectionDue && o.BeehiveId is int b && hiveApiary.ContainsKey(b))
                     .GroupBy(o => (o.Date, ApiaryId: hiveApiary[o.BeehiveId!.Value])))
        {
            var list = group.ToList();
            result.Add(list.Count == 1
                ? new DashboardObligationDto(group.Key.Date, nameof(ObligationKind.InspectionDue), list[0].Title, LinkFor(list[0].BeehiveId, null))
                : new DashboardObligationDto(group.Key.Date, nameof(ObligationKind.InspectionDue),
                    $"🔍 Preporučeni pregled — {apiaryNames.GetValueOrDefault(group.Key.ApiaryId, string.Empty)} ({list.Count})",
                    LinkFor(null, group.Key.ApiaryId)));
        }

        return result.OrderBy(o => o.Date).ThenBy(o => o.Kind).ToList();
    }

    // ── Programi u toku ─────────────────────────────────────────────────────────

    private List<ProgrammeDto> Programmes(
        DateTime now, Dictionary<int, string> apiaryNames, List<Treatment> treatments, List<Diet> diets)
    {
        var stripDays = GetInt("Alerts:StripRemovalDays", 42);
        var result = new List<ProgrammeDto>();

        foreach (var d in diets.Where(d => d.Status == DietStatus.InProgress && d.Beehives.Any(db => db.RemovedOn == null)))
        {
            var next = d.FeedingEntries.Where(e => e.Status == FeedingEntryStatus.Pending).OrderBy(e => e.ScheduledDate).FirstOrDefault();
            result.Add(new ProgrammeDto("Feeding", d.Id, apiaryNames.GetValueOrDefault(d.ApiaryId, string.Empty), d.Name,
                d.FeedingEntries.Count(e => e.Status == FeedingEntryStatus.Completed), d.FeedingEntries.Count,
                next is null ? null : _seasons.LocalDate(next.ScheduledDate), $"/feedings/{d.Id}"));
        }

        foreach (var t in treatments)
        {
            var apiary = apiaryNames.GetValueOrDefault(t.ApiaryId, string.Empty);
            var link = $"/treatments/{t.Id}";

            if (t.Rounds.Count > 1 && t.Rounds.Any(r => r.Status == TreatmentRoundStatus.Pending))
            {
                var next = t.Rounds.Where(r => r.Status == TreatmentRoundStatus.Pending).Min(r => r.ScheduledDate);
                result.Add(new ProgrammeDto("TreatmentRounds", t.Id, apiary, t.ProductName,
                    t.Rounds.Count(r => r.Status == TreatmentRoundStatus.Completed), t.Rounds.Count,
                    _seasons.LocalDate(next), link));
            }

            if (t.Method == ApplicationMethod.Strips && t.EndDate is null)
                result.Add(new ProgrammeDto("Strips", t.Id, apiary, t.ProductName, null, null,
                    _seasons.LocalDate(t.StartDate.AddDays(stripDays)), link));

            if (t.EndDate is not null && t.WithdrawalDays > 0)
            {
                var until = TreatmentStatusHelper.KarencaUntil(t.StartDate, t.EndDate, t.WithdrawalDays);
                if (until > now)
                    result.Add(new ProgrammeDto("Karenca", t.Id, apiary, t.ProductName, null, null,
                        _seasons.LocalDate(until), link));
            }
        }

        return result.OrderBy(p => p.Date ?? DateOnly.MaxValue).ToList();
    }

    // ── Prinos ──────────────────────────────────────────────────────────────────

    private async Task<(List<MonthYieldDto> ByMonth, decimal ThisYear, decimal LastYearToDate)> YieldAsync(
        List<int> apiaryIds, HashSet<int> hiveSet, bool isBeekeeper, DateOnly today)
    {
        // Honey only (SPEC-30): the card is "prinos meda". The organization's own records (no apiary)
        // belong to whoever sees the whole organization — its owner.
        var harvests = (await _uow.Harvests.GetByApiariesAsync(apiaryIds, HarvestKind.Honey)).ToList();
        if (_currentUser.Role == UserRole.OrganizationAdmin && _currentUser.OrganizationId is int orgId)
            harvests.AddRange(await _uow.Harvests.GetSharedAsync(orgId, HarvestKind.Honey));
        var locked = await _planLock.GetForCurrentUserAsync();

        // A Beekeeper counts their own hives only. Managers count the whole apiary — including hives
        // since merged away, whose honey was still harvested — minus anything the plan has locked.
        bool Counts(HarvestEntry e) => isBeekeeper ? hiveSet.Contains(e.BeehiveId) : !locked.BeehiveIds.Contains(e.BeehiveId);

        // A record kept as one figure counts whole for a manager; it has no hive, so it is never a
        // beekeeper's — their chart is their own hives.
        decimal KgOf(Harvest h) => h.BulkKg is decimal bulk
            ? (isBeekeeper ? 0m : bulk)
            : h.Entries.Where(Counts).Sum(e => e.QuantityKg);

        var thisYear = new decimal[13];
        var lastYear = new decimal[13];
        var lastYearToDate = 0m;
        var sameDayLastYear = today.AddYears(-1);

        foreach (var h in harvests)
        {
            var day = _seasons.LocalDate(h.Date);
            var kg = KgOf(h);
            if (kg == 0) continue;

            if (day.Year == today.Year) thisYear[day.Month] += kg;
            else if (day.Year == today.Year - 1)
            {
                lastYear[day.Month] += kg;
                if (day <= sameDayLastYear) lastYearToDate += kg;
            }
        }

        var byMonth = Enumerable.Range(1, 12)
            .Where(m => thisYear[m] > 0 || lastYear[m] > 0)
            .Select(m => new MonthYieldDto(m, thisYear[m], lastYear[m]))
            .ToList();

        return (byMonth, thisYear.Sum(), lastYearToDate);
    }

    // ── Zadaci, Edukacija ─────────────────────────────────────────────────────────

    private List<DashboardTodoDto> TopTodos(
        List<Todos.DTOs.TodoDto> todos, DateOnly today, IReadOnlyList<Beehive> hives, Dictionary<int, string> apiaryNames)
    {
        var hiveNames = hives.ToDictionary(h => h.Id, h => h.Name);

        return todos
            .OrderByDescending(t => IsOverdue(t.DueDate, today))
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(t => t.Priority)
            .Take(TodosShown)
            .Select(t => new DashboardTodoDto(
                t.Id, t.Title, t.DueDate, t.Priority, IsOverdue(t.DueDate, today),
                t.BeehiveId is int b && hiveNames.TryGetValue(b, out var hn) ? hn
                    : t.ApiaryId is int a && apiaryNames.TryGetValue(a, out var an) ? an : null,
                LinkFor(t.BeehiveId, t.ApiaryId)))
            .ToList();
    }

    private async Task<DashboardTopicDto?> TopicAsync(DateOnly today)
    {
        var topics = (await _uow.LearningTopics.GetPublishedAsync(month: today.Month)).ToList();
        if (topics.Count == 0 || _currentUser.UserId is not int userId) return null;

        var read = await _uow.LearningTopics.GetReadTopicIdsAsync(userId);
        var pick = topics
            .OrderBy(t => read.Contains(t.Id))
            .ThenByDescending(t => t.PublishedAt)
            .First();
        return new DashboardTopicDto(pick.Id, pick.Title, pick.Summary, pick.Category);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>A SystemAdmin has no organization, hence no season and no hives: 403, as on /organization.</summary>
    private async Task<Organization> LoadOrganizationAsync()
    {
        if (_currentUser.OrganizationId is not int orgId)
            throw new ForbiddenAccessException("Vaš račun ne pripada nijednoj organizaciji.");

        return await _uow.Organizations.GetByIdAsync(orgId)
            ?? throw new NotFoundException(nameof(Organization), orgId);
    }

    private bool IsOverdue(DateTime? dueDate, DateOnly today) =>
        dueDate is DateTime due && _seasons.LocalDate(due) < today;

    private static bool SameMonth(DateOnly a, DateOnly b) => a.Year == b.Year && a.Month == b.Month;

    private static string LinkFor(int? beehiveId, int? apiaryId) =>
        beehiveId is int b ? $"/beehives/{b}" : apiaryId is int a ? $"/apiaries/{a}" : "/calendar";

    private static IReadOnlyList<string> Cap(List<string> items) =>
        items.Count <= ItemsShown ? items : [.. items.Take(ItemsShown), $"… i još {items.Count - ItemsShown}"];

    private int GetInt(string key, int fallback) => int.TryParse(_config[key], out var v) ? v : fallback;
}
