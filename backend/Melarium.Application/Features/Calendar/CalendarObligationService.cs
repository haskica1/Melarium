using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Calendar;

/// <summary>
/// Builds the flattened obligation list (feedings, todos, derived treatment + inspection deadlines)
/// for a resolved calendar user over a date window. Derived-deadline rules reuse the same thresholds
/// as the SPEC-04 alert scan (<c>Alerts:StripRemovalDays</c>, <c>Alerts:StaleInspectionDays</c>).
/// </summary>
public class CalendarObligationService : ICalendarObligationService
{
    private readonly IUnitOfWork _uow;
    private readonly ICalendarAccessResolver _resolver;
    private readonly IConfiguration _config;

    public CalendarObligationService(IUnitOfWork uow, ICalendarAccessResolver resolver, IConfiguration config)
    {
        _uow      = uow;
        _resolver = resolver;
        _config   = config;
    }

    public async Task<IReadOnlyList<CalendarObligation>> GatherAsync(
        CalendarUserContext ctx, DateOnly from, DateOnly to, CalendarCategories categories)
    {
        var scope   = await _resolver.ResolveAsync(ctx);
        var result  = new List<CalendarObligation>();
        var baseUrl = (_config["App:PublicBaseUrl"] ?? string.Empty).TrimEnd('/');

        string? Link(int? beehiveId, int? apiaryId)
        {
            if (baseUrl.Length == 0) return null;
            if (beehiveId is int b) return $"{baseUrl}/beehives/{b}";
            if (apiaryId is int a)  return $"{baseUrl}/apiaries/{a}";
            return $"{baseUrl}/calendar";
        }

        bool InRange(DateOnly d) => d >= from && d <= to;

        // ── Feedings (pending rounds of non-stopped diets) ───────────────────────
        // A round is one visit covering every hive on the programme, so it produces ONE obligation
        // per date whatever the hive count — that de-duplication is the point of SPEC-12.
        if (categories.Feedings && scope.ApiaryIds.Count > 0 && scope.BeehiveIds.Count > 0)
        {
            var diets = (await _uow.Diets.GetByApiaryIdsAsync(scope.ApiaryIds))
                .Where(d => d.Status != DietStatus.StoppedEarly)
                // Narrow to programmes covering a hive the caller actually has. A Beekeeper's
                // ApiaryIds are the apiaries *containing* their hives, so without this a colleague's
                // programme would be pushed into their personal calendar. A programme with zero
                // active hives drops out here too — there is nothing to do in the field.
                .Where(d => d.Beehives.Any(db => db.RemovedOn == null && scope.BeehiveIds.Contains(db.BeehiveId)))
                .ToList();

            foreach (var d in diets)
            {
                var foodName   = d.FoodType == FoodType.Custom
                    ? (d.CustomFoodType ?? "Vlastito")
                    : BsLabels.Label(d.FoodType);
                var apiaryName = scope.ApiaryNames.TryGetValue(d.ApiaryId, out var an) ? an : $"Pčelinjak {d.ApiaryId}";
                var hiveCount  = d.Beehives.Count(db => db.RemovedOn == null);

                foreach (var e in d.FeedingEntries.Where(e => e.Status == FeedingEntryStatus.Pending))
                {
                    var date = DateOnly.FromDateTime(e.ScheduledDate);
                    if (!InRange(date)) continue;

                    var desc = $"Program: {d.Name}\nHrana: {foodName}\nKošnice: {hiveCount}";
                    // The programme page, not the apiary: it is the checklist the user needs to tick.
                    if (baseUrl.Length > 0) desc += $"\nOtvori: {baseUrl}/feedings/{d.Id}";

                    // StableKey stays "feeding-{entryId}": the ICS UID is derived from it, so events
                    // already synced to a real calendar update in place instead of duplicating.
                    result.Add(new CalendarObligation(
                        ObligationKind.Feeding, $"feeding-{e.Id}", date,
                        $"🍯 Prehrana — {apiaryName} ({hiveCount} {HiveWord(hiveCount)})",
                        desc, apiaryName, null, d.ApiaryId, false));
                }
            }
        }

        // ── Todos with a due date ────────────────────────────────────────────────
        if (categories.Todos)
        {
            List<Todo> todos;
            if (ctx.Role == UserRole.Beekeeper)
                todos = (await _uow.Todos.FindAsync(t => t.AssignedToId == ctx.UserId)).ToList();
            else if (scope.ApiaryIds.Count > 0 || scope.BeehiveIds.Count > 0)
                todos = (await _uow.Todos.FindAsync(t =>
                    (t.ApiaryId.HasValue  && scope.ApiaryIds.Contains(t.ApiaryId.Value)) ||
                    (t.BeehiveId.HasValue && scope.BeehiveIds.Contains(t.BeehiveId.Value)))).ToList();
            else
                todos = new List<Todo>();

            foreach (var t in todos.Where(t => t.DueDate.HasValue && !t.IsCompleted))
            {
                var date = DateOnly.FromDateTime(t.DueDate!.Value);
                if (!InRange(date)) continue;

                var scopeName =
                    t.BeehiveId.HasValue && scope.BeehiveNames.TryGetValue(t.BeehiveId.Value, out var bn) ? bn :
                    t.ApiaryId.HasValue  && scope.ApiaryNames.TryGetValue(t.ApiaryId.Value, out var an)   ? an : null;

                var title = scopeName != null ? $"📋 {t.Title} — {scopeName}" : $"📋 {t.Title}";
                var desc  = t.Notes;
                var link  = Link(t.BeehiveId, t.ApiaryId);
                if (link != null) desc = string.IsNullOrWhiteSpace(desc) ? $"Otvori: {link}" : $"{desc}\nOtvori: {link}";

                result.Add(new CalendarObligation(
                    ObligationKind.Todo, $"todo-{t.Id}", date, title, desc, scopeName, t.BeehiveId, t.ApiaryId, false));
            }
        }

        // ── Derived treatment deadlines: strip removal + karenca end (SPEC-08) ────
        if (categories.Treatments && ctx.OrganizationId is int treatOrg)
        {
            var stripDays = GetInt("Alerts:StripRemovalDays", 42);
            var treatments = (await _uow.Treatments.GetByOrganizationAsync(treatOrg))
                .Where(t => scope.ApiaryIds.Contains(t.ApiaryId))
                .ToList();

            foreach (var t in treatments)
            {
                var apiaryName = scope.ApiaryNames.TryGetValue(t.ApiaryId, out var an) ? an : $"Pčelinjak {t.ApiaryId}";
                var link = Link(null, t.ApiaryId);

                if (t.Method == ApplicationMethod.Strips && t.EndDate is null)
                {
                    var date = DateOnly.FromDateTime(t.StartDate.AddDays(stripDays));
                    if (InRange(date))
                    {
                        var desc = $"Preparat: {t.ProductName}";
                        if (link != null) desc += $"\nOtvori: {link}";
                        result.Add(new CalendarObligation(
                            ObligationKind.StripRemoval, $"strips-{t.Id}", date,
                            $"💊 Izvadi trake — {apiaryName}", desc, apiaryName, null, t.ApiaryId, false));
                    }
                }

                if (t.EndDate is not null && t.WithdrawalDays > 0)
                {
                    var karencaUntil = TreatmentStatusHelper.KarencaUntil(t.StartDate, t.EndDate, t.WithdrawalDays);
                    var date = DateOnly.FromDateTime(karencaUntil);
                    if (InRange(date))
                    {
                        var desc = $"Preparat: {t.ProductName} — nakon ovog datuma med se ponovo smije vrcati.";
                        if (link != null) desc += $"\nOtvori: {link}";
                        result.Add(new CalendarObligation(
                            ObligationKind.KarencaEnd, $"karenca-{t.Id}", date,
                            $"💊 Istekla karenca — {apiaryName}", desc, apiaryName, null, t.ApiaryId, false));
                    }
                }
            }
        }

        // ── Derived recommended inspection (soft, recomputed each run) ────────────
        if (categories.Inspections && scope.BeehiveIds.Count > 0)
        {
            var staleDays = GetInt("Alerts:StaleInspectionDays", 21);
            var hiveIds = scope.BeehiveIds;

            var lastByHive = (await _uow.Inspections.FindAsync(i => hiveIds.Contains(i.BeehiveId)))
                .GroupBy(i => i.BeehiveId)
                .ToDictionary(g => g.Key, g => g.Max(i => i.Date));

            var hives = (await _uow.Beehives.FindAsync(b => hiveIds.Contains(b.Id))).ToList();
            foreach (var hive in hives)
            {
                var last = lastByHive.TryGetValue(hive.Id, out var d) ? d : hive.CreatedAt;
                var date = DateOnly.FromDateTime(last.AddDays(staleDays));
                if (!InRange(date)) continue;

                var hiveName = scope.BeehiveNames.TryGetValue(hive.Id, out var hn) ? hn : hive.Name;
                var link = Link(hive.Id, null);
                var desc = "Preporučeni pregled — nema pregleda u zadatom periodu.";
                if (link != null) desc += $"\nOtvori: {link}";

                result.Add(new CalendarObligation(
                    ObligationKind.InspectionDue, $"inspection-{hive.Id}", date,
                    $"🔍 Preporučeni pregled — {hiveName}", desc, hiveName, hive.Id, null, true));
            }
        }

        return result
            .OrderBy(o => o.Date)
            .ThenBy(o => o.Kind)
            .ToList();
    }

    /// <summary>Bosnian plural of "košnica": 1 → košnica, 2–4 → košnice, else košnica (with 11–14 exception).</summary>
    private static string HiveWord(int n)
    {
        var mod100 = n % 100;
        var mod10  = n % 10;
        if (mod10 == 1 && mod100 != 11) return "košnica";
        if (mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14) return "košnice";
        return "košnica";
    }

    private int GetInt(string key, int fallback) => int.TryParse(_config[key], out var v) ? v : fallback;
}
