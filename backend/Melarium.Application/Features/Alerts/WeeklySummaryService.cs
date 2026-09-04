using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Inspections.Groq;
using Melarium.Domain.Common;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Weather;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Alerts;

/// <summary>
/// Builds a deterministic weekly digest per organization and asks Groq (Llama 3.3 70B) to write a
/// short Bosnian summary from it, then delivers it as a <c>WeeklySummary</c> notification to the
/// organization's admins. Reuses the existing Groq stack — no new AI provider (SPEC-04 Part B).
/// </summary>
public class WeeklySummaryService : IWeeklySummaryService
{
    private readonly HttpClient _http;
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly IWeatherService _weather;
    private readonly IConfiguration _config;
    private readonly Common.Security.IPlanLock _planLock;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private const string SystemMessage =
        """
        Ti si stručni pčelarski asistent koji piše sažet sedmični izvještaj za pčelara na bosanskom jeziku.
        Koristi ISKLJUČIVO činjenice iz priloženih podataka — ništa ne izmišljaj i ne dodaj brojke kojih nema.
        Napiši 5 do 8 kratkih stavki (bullet lista), svaku u zasebnom redu koji počinje sa "- ".
        Počni s najvažnijom, akcijskom stavkom (npr. zakašnjeli zadaci, mraz, opadanje meda).
        Ton: prijateljski i profesionalan. Ne koristi markdown naslove niti uvod — samo listu stavki.
        """;

    public WeeklySummaryService(
        HttpClient http,
        IUnitOfWork uow,
        INotificationService notifications,
        IWeatherService weather,
        IConfiguration config,
        Common.Security.IPlanLock planLock)
    {
        _http = http;
        _uow = uow;
        _notifications = notifications;
        _weather = weather;
        _config = config;
        _planLock = planLock;

        var apiKey = config["Groq:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (!GetBool("Alerts:WeeklySummary:Enabled", true)) return;
        if (string.IsNullOrWhiteSpace(_config["Groq:ApiKey"])) return; // no AI configured → skip

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var orgs = await _uow.Organizations.GetAllAsync();

        foreach (var org in orgs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Weekly AI summary is a paid-plan feature (SPEC-09) — no Groq call for Free orgs.
            if (PlanHelper.Effective(org.Plan, org.PlanValidUntil, DateTime.UtcNow) < PlanType.Standard)
                continue;

            // A Standard organization that came down from Pro can still have locked apiaries — the
            // summary must not describe hives the reader cannot open (SPEC-24).
            var locked = await _planLock.GetForOrganizationAsync(org.Id);

            var apiaries = (await _uow.Apiaries.GetAllByOrganizationAsync(org.Id))
                .Where(a => !locked.ApiaryIds.Contains(a.Id))
                .ToList();
            if (apiaries.Count == 0) continue;
            var apiaryIds = apiaries.Select(a => a.Id).ToList();

            var input = await GatherAsync(org, apiaries, weekAgo, locked);
            if (!input.HasActivity) continue; // no noise for idle organizations

            string bullets;
            try
            {
                bullets = await GenerateSummaryAsync(input);
            }
            catch
            {
                continue; // AI failure → skip this org silently (summary is nice-to-have)
            }
            if (string.IsNullOrWhiteSpace(bullets)) continue;

            // Recipients: OrganizationAdmins + ApiaryAdmins of the org.
            var recipients = new HashSet<int>();
            recipients.UnionWith(await _uow.Users.GetOrganizationAdminIdsAsync(org.Id));
            foreach (var apiaryId in apiaryIds)
                recipients.UnionWith(await _uow.Users.GetApiaryAdminIdsAsync(apiaryId));

            var since = DateTime.UtcNow.AddDays(-6); // guard against a double-run on the same Monday
            foreach (var userId in recipients)
            {
                if (await _uow.Notifications.ExistsRecentAsync(userId, NotificationType.WeeklySummary, org.Id, since))
                    continue;

                await _notifications.NotifyAsync(
                    userId, "Sedmični pregled", bullets,
                    NotificationType.WeeklySummary, org.Id, nameof(Organization));
            }
        }
    }

    // ── Deterministic data gathering ─────────────────────────────────────────────

    private async Task<WeeklyDigestInput> GatherAsync(Organization org, List<Apiary> apiaries, DateTime weekAgo, PlanLockResult locked)
    {
        var now = DateTime.UtcNow;
        var apiaryIds = apiaries.Select(a => a.Id).ToList();
        var apiaryNames = apiaries.ToDictionary(a => a.Id, a => a.Name);

        var beehives = (await _uow.Beehives.GetByOrganizationAsync(org.Id))
            .Where(b => !locked.BeehiveIds.Contains(b.Id))
            .ToList();
        var hiveIds = beehives.Select(b => b.Id).ToList();
        var hiveNames = beehives.ToDictionary(b => b.Id, b => b.Name);
        var hiveApiary = beehives.ToDictionary(b => b.Id, b => b.ApiaryId);

        var inspections = hiveIds.Count > 0
            ? (await _uow.Inspections.FindAsync(i => hiveIds.Contains(i.BeehiveId) && i.Date >= weekAgo)).ToList()
            : [];

        var highlights = inspections
            .Where(i => !string.IsNullOrWhiteSpace(i.BroodStatus) || !string.IsNullOrWhiteSpace(i.Notes))
            .OrderByDescending(i => i.Date)
            .Take(10)
            .Select(i =>
            {
                var name = hiveNames.TryGetValue(i.BeehiveId, out var n) ? n : $"Košnica {i.BeehiveId}";
                var text = !string.IsNullOrWhiteSpace(i.BroodStatus) ? i.BroodStatus! : i.Notes!;
                return $"{name} ({i.Date:dd.MM}): {Truncate(text, 120)}";
            })
            .ToList();

        // Honey-level trend per apiary: this week's inspection count + most recent level.
        var honeyTrend = inspections
            .GroupBy(i => hiveApiary.TryGetValue(i.BeehiveId, out var ap) ? ap : 0)
            .Where(g => g.Key != 0)
            .Select(g =>
            {
                var apName = apiaryNames.TryGetValue(g.Key, out var n) ? n : $"Pčelinjak {g.Key}";
                var latest = g.OrderByDescending(i => i.Date).First();
                return $"{apName}: {g.Count()} pregleda, zadnji nivo meda: {BsLabel(latest.HoneyLevel)}";
            })
            .ToList();

        // Counts feeding *rounds*, not hive-rounds: one round covers every hive on the programme.
        var feedingsDone = apiaryIds.Count > 0
            ? (await _uow.FeedingEntries.FindAsync(fe =>
                fe.Status == FeedingEntryStatus.Completed &&
                fe.CompletionDate >= weekAgo &&
                apiaryIds.Contains(fe.Diet.ApiaryId))).Count()
            : 0;

        // Same "one round, whatever the hive count" counting as feedings above.
        var treatmentRoundsDone = apiaryIds.Count > 0
            ? (await _uow.Treatments.GetByApiaryIdsAsync(apiaryIds))
                .SelectMany(t => t.Rounds)
                .Count(r => r.Status == TreatmentRoundStatus.Completed && r.CompletionDate >= weekAgo)
            : 0;

        var todos = (apiaryIds.Count > 0 || hiveIds.Count > 0)
            ? (await _uow.Todos.FindAsync(t =>
                (t.ApiaryId.HasValue && apiaryIds.Contains(t.ApiaryId.Value)) ||
                (t.BeehiveId.HasValue && hiveIds.Contains(t.BeehiveId.Value)))).ToList()
            : [];

        var todosCreated = todos.Count(t => t.CreatedAt >= weekAgo);
        var todosCompleted = todos.Count(t => t.IsCompleted && t.CompletedAt >= weekAgo);
        var todosOverdue = todos.Count(t => !t.IsCompleted && t.DueDate.HasValue && t.DueDate.Value < now);

        var harvests = apiaryIds.Count > 0
            ? (await _uow.Harvests.GetByApiariesAsync(apiaryIds)).Where(h => h.Date >= weekAgo).ToList()
            : [];
        var harvestKg = harvests.Sum(h => h.Entries.Sum(e => e.QuantityKg));

        var weatherOutlook = new List<string>();
        foreach (var apiary in apiaries)
        {
            if (apiary.Latitude is not double lat || apiary.Longitude is not double lon) continue;
            try
            {
                var forecast = await _weather.GetForecastAsync(lat, lon);
                var day = forecast.Daily.Skip(1).FirstOrDefault() ?? forecast.Daily.FirstOrDefault();
                if (day is null) continue;
                var rain = day.PrecipitationProbability.HasValue ? $", kiša {day.PrecipitationProbability:0}%" : "";
                weatherOutlook.Add($"{apiary.Name}: {day.MinTemp:0}–{day.MaxTemp:0}°C{rain}");
            }
            catch { /* weather unavailable for this apiary → skip its line */ }
        }

        return new WeeklyDigestInput(
            org.Name, inspections.Count, highlights, feedingsDone, treatmentRoundsDone,
            todosCreated, todosCompleted, todosOverdue, harvestKg, honeyTrend, weatherOutlook);
    }

    // ── Groq call ────────────────────────────────────────────────────────────────

    private async Task<string> GenerateSummaryAsync(WeeklyDigestInput input)
    {
        var digest = WeeklyDigestBuilder.Build(input);

        var requestBody = new
        {
            model = Ai.GroqModels.Chat(_config),
            temperature = 0.3,
            max_tokens = 700,
            messages = new[]
            {
                new { role = "system", content = SystemMessage },
                new { role = "user",   content = $"Podaci za proteklu sedmicu:\n\n{digest}\n\nNapiši sedmični pregled kao listu stavki." },
            },
        };

        var response = await _http.PostAsJsonAsync("https://api.groq.com/openai/v1/chat/completions", requestBody);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<GroqChatResponse>(JsonOpts)
            ?? throw new InvalidOperationException("Empty response from Groq chat API.");

        return raw.Choices?[0].Message?.Content?.Trim()
            ?? throw new InvalidOperationException("No content in Groq chat response.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max].TrimEnd() + "…";

    private static string BsLabel(HoneyLevel level) => level switch
    {
        HoneyLevel.Low    => "nizak",
        HoneyLevel.Medium => "srednji",
        HoneyLevel.High   => "visok",
        _                 => level.ToString(),
    };

    private bool GetBool(string key, bool fallback) => bool.TryParse(_config[key], out var v) ? v : fallback;
}
