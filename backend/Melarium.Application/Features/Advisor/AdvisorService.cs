using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Advisor.DTOs;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Weather;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Advisor;

/// <summary>
/// AI beekeeping advisor (SPEC-01): personal chat conversations, optionally grounded in a hive's real
/// data. Ownership is enforced from the JWT-derived <see cref="ICurrentUser"/>; the AI call is
/// transactional — messages are persisted only after a successful reply.
/// </summary>
public class AdvisorService : IAdvisorService
{
    private const int MaxMessages = 60;
    private const int HistoryWindow = 12;

    private readonly IUnitOfWork _uow;
    private readonly IAccessGuard _access;
    private readonly ICurrentUser _currentUser;
    private readonly IAdvisorAiClient _ai;
    private readonly IWeatherService _weather;
    private readonly ITranscriptionService _transcription;
    private readonly IPlanGuard _plan;

    public AdvisorService(
        IUnitOfWork uow,
        IAccessGuard access,
        ICurrentUser currentUser,
        IAdvisorAiClient ai,
        IWeatherService weather,
        ITranscriptionService transcription,
        IPlanGuard plan)
    {
        _uow = uow;
        _access = access;
        _currentUser = currentUser;
        _ai = ai;
        _weather = weather;
        _transcription = transcription;
        _plan = plan;
    }

    private const string SystemPrompt =
        """
        Ti si stručan pčelarski savjetnik za Bosnu i Hercegovinu i regiju. Pomažeš pčelaru praktičnim,
        konkretnim savjetima. Tečno poznaješ pčelarsku terminologiju i žargon na bosanskom, hrvatskom i
        srpskom jeziku (matica, leglo, poklopljeno leglo, okviri/ramovi, medište, nastavak, roj, rojenje,
        matičnjaci, propolis, pelud, varoa/varooza, nozemoza, američka/europska gnjiloća, krečno leglo,
        oksalna/mravlja kiselina, amitraz).

        PRAVILA:
        1. Odgovaraj ISKLJUČIVO na bosanskom jeziku, jasno i sažeto (do ~300 riječi, osim ako korisnik
           izričito traži više).
        2. Formatiraj odgovor Markdownom radi preglednosti: kratak uvodni sažetak (1–2 rečenice), zatim
           **podebljani** ključni pojmovi i numerisana lista konkretnih koraka kada su u pitanju radnje.
           Izbjegavaj duge blokove teksta bez strukture.
        3. Budi konkretan i praktičan — navedi količine, vremenske okvire i sezonski kontekst umjesto
           uopštenih fraza. Prilagodi savjet dobu godine.
        4. Budi iskren o nesigurnosti — ako nešto ovisi o pregledu ili dodatnim podacima, reci to i
           predloži šta pčelar treba provjeriti.
        5. Nisi veterinar. Kod sumnje na američku ili europsku gnjiloću (AFB/EFB) OBAVEZNO naglasi da je
           to bolest koja PODLIJEŽE OBAVEZNOJ PRIJAVI nadležnoj veterinarskoj inspekciji.
        6. Ne preporučuj doziranje lijekova mimo uputstva proizvođača.
        7. Ako su niže priloženi podaci o košnici, koristi ih i referiši se na njih; ništa ne izmišljaj
           izvan tih podataka.
        8. Ako pitanje nije vezano za pčelarstvo, ljubazno i kratko odbij i vrati razgovor na pčelarstvo.
        """;

    // ── Queries ──────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<AdvisorConversationSummaryDto>> GetConversationsAsync()
    {
        var userId = RequireUser();
        var conversations = await _uow.AdvisorConversations.GetByUserAsync(userId);
        return conversations.Select(ToSummary);
    }

    public async Task<AdvisorConversationDetailDto> GetConversationAsync(int id)
    {
        var userId = RequireUser();
        var conversation = await _uow.AdvisorConversations.GetWithMessagesAsync(id);
        if (conversation is null || conversation.UserId != userId)
            throw new NotFoundException(nameof(AdvisorConversation), id);
        return ToDetail(conversation);
    }

    // ── Commands ─────────────────────────────────────────────────────────────────

    public async Task<AdvisorConversationDetailDto> CreateConversationAsync(CreateConversationDto dto)
    {
        var userId = RequireUser();
        await EnsurePlanAllowsMessageAsync();
        var message = dto.Message.Trim();

        string? contextBlock = null;
        if (dto.BeehiveId is int beehiveId)
        {
            await _access.EnsureCanAccessBeehiveAsync(beehiveId);
            contextBlock = await BuildContextBlockAsync(beehiveId);
        }

        var chat = new List<ChatMessage>
        {
            new("system", BuildSystemPrompt(contextBlock)),
            new("user", message),
        };
        var reply = await CallAiAsync(chat);

        var conversation = new AdvisorConversation
        {
            UserId    = userId,
            BeehiveId = dto.BeehiveId,
            Title     = TitleFrom(message),
            Messages  =
            {
                new AdvisorMessage { Role = AdvisorRole.User,      Content = message },
                new AdvisorMessage { Role = AdvisorRole.Assistant, Content = reply },
            },
        };

        await _uow.AdvisorConversations.AddAsync(conversation);
        await _uow.SaveChangesAsync();

        var saved = await _uow.AdvisorConversations.GetWithMessagesAsync(conversation.Id);
        return ToDetail(saved!);
    }

    public async Task<AdvisorMessagePairDto> SendMessageAsync(int conversationId, SendMessageDto dto)
    {
        var userId = RequireUser();
        await EnsurePlanAllowsMessageAsync();
        var message = dto.Message.Trim();

        var conversation = await _uow.AdvisorConversations.GetWithMessagesAsync(conversationId);
        if (conversation is null || conversation.UserId != userId)
            throw new NotFoundException(nameof(AdvisorConversation), conversationId);

        if (conversation.Messages.Count >= MaxMessages)
            throw new BusinessRuleException("Ovaj razgovor je dostigao maksimalnu dužinu. Započni novi razgovor.");

        // Rebuild context each turn (best-effort — a since-revoked hive just drops the context).
        string? contextBlock = null;
        if (conversation.BeehiveId is int beehiveId && await _access.CanAccessBeehiveAsync(beehiveId))
            contextBlock = await BuildContextBlockAsync(beehiveId);

        var chat = new List<ChatMessage> { new("system", BuildSystemPrompt(contextBlock)) };
        foreach (var m in conversation.Messages.TakeLast(HistoryWindow))
            chat.Add(new ChatMessage(m.Role == AdvisorRole.User ? "user" : "assistant", m.Content));
        chat.Add(new ChatMessage("user", message));

        var reply = await CallAiAsync(chat);

        var userMsg      = new AdvisorMessage { Role = AdvisorRole.User,      Content = message };
        var assistantMsg = new AdvisorMessage { Role = AdvisorRole.Assistant, Content = reply };
        conversation.Messages.Add(userMsg);
        conversation.Messages.Add(assistantMsg);
        conversation.UpdatedAt = DateTime.UtcNow; // bumps "last activity" for the list ordering

        await _uow.AdvisorConversations.UpdateAsync(conversation);
        await _uow.SaveChangesAsync();

        return new AdvisorMessagePairDto(ToMessageDto(userMsg), ToMessageDto(assistantMsg));
    }

    public async Task DeleteConversationAsync(int id)
    {
        var userId = RequireUser();
        var conversation = await _uow.AdvisorConversations.GetByIdAsync(id);
        if (conversation is null || conversation.UserId != userId)
            throw new NotFoundException(nameof(AdvisorConversation), id);

        await _uow.AdvisorConversations.DeleteAsync(conversation);
        await _uow.SaveChangesAsync();
    }

    public async Task<string> TranscribeAsync(Stream audioStream, string fileName)
    {
        // Transcription alone doesn't consume the message quota — it's gated as voice input;
        // the transcribed text still goes through the quota'd send path.
        if (_currentUser.OrganizationId is int orgId)
            await _plan.EnsureFeatureAsync(orgId, PlanFeature.VoiceInput);

        var transcript = await _transcription.TranscribeAsync(audioStream, fileName);
        if (string.IsNullOrWhiteSpace(transcript))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["audio"] = ["Nije prepoznat govor u snimku. Pokušaj ponovo."]
            });
        return transcript.Trim();
    }

    /// <summary>Plan gate + monthly quota (SPEC-09); org-less callers (SystemAdmin) pass through.</summary>
    private async Task EnsurePlanAllowsMessageAsync()
    {
        if (_currentUser.OrganizationId is int orgId)
            await _plan.EnsureAdvisorMessageAsync(orgId);
    }

    // ── AI call (transactional guard) ────────────────────────────────────────────

    private async Task<string> CallAiAsync(IReadOnlyList<ChatMessage> chat)
    {
        try
        {
            var reply = await _ai.SendAsync(chat);
            if (string.IsNullOrWhiteSpace(reply))
                throw new InvalidOperationException("Empty AI reply.");
            return reply.Trim();
        }
        catch (Exception)
        {
            // Nothing is persisted by the callers before this returns, so a failure loses no data.
            throw new BusinessRuleException("AI servis trenutno nije dostupan. Pokušaj ponovo za koji trenutak.");
        }
    }

    private static string BuildSystemPrompt(string? contextBlock) =>
        string.IsNullOrWhiteSpace(contextBlock) ? SystemPrompt : $"{SystemPrompt}\n\n{contextBlock}";

    // ── Context assembly ─────────────────────────────────────────────────────────

    private async Task<string?> BuildContextBlockAsync(int beehiveId)
    {
        var hive = await _uow.Beehives.GetByIdAsync(beehiveId);
        if (hive is null) return null; // hive deleted → general answer

        var apiary = await _uow.Apiaries.GetByIdAsync(hive.ApiaryId);
        var apiaryName = apiary?.Name ?? "—";

        var inspections = (await _uow.Inspections.GetByBeehiveIdAsync(beehiveId)).Take(5).ToList();

        var diets = await _uow.Diets.GetByBeehiveIdAsync(beehiveId);
        var activeDiet = diets.FirstOrDefault(d => d.Status == DietStatus.InProgress)
                      ?? diets.FirstOrDefault(d => d.Status == DietStatus.NotStarted);
        int dietCompleted = 0, dietTotal = 0;
        if (activeDiet is not null)
        {
            var withEntries = await _uow.Diets.GetWithEntriesAsync(activeDiet.Id);
            if (withEntries is not null)
            {
                dietTotal = withEntries.FeedingEntries.Count;
                dietCompleted = withEntries.FeedingEntries.Count(e => e.Status == FeedingEntryStatus.Completed);
            }
        }

        var openTodos = (await _uow.Todos.GetByBeehiveIdAsync(beehiveId))
            .Where(t => !t.IsCompleted).Take(5).ToList();

        var queen = await _uow.Queens.GetActiveByBeehiveIdAsync(beehiveId);

        var yearly = await _uow.Harvests.GetHiveYearlyTotalsAsync(beehiveId);
        decimal? seasonYield = yearly.TryGetValue(DateTime.UtcNow.Year, out var kg) ? kg : null;

        var latestTreatment = (await _uow.Treatments.GetLatestForBeehivesAsync([beehiveId]))
            .GetValueOrDefault(beehiveId);

        var latestMove = await _uow.ApiaryMoves.GetLatestForApiaryAsync(hive.ApiaryId);
        string? pastureLine = latestMove?.ToPasture is not null
            ? $"{latestMove.ToPasture.Name}, od {latestMove.MovedAt:dd.MM.yyyy}"
            : null;

        string? weatherLine = null;
        if (apiary?.Latitude is double lat && apiary.Longitude is double lon)
        {
            try
            {
                var forecast = await _weather.GetForecastAsync(lat, lon);
                var today = forecast.Daily.FirstOrDefault();
                if (today is not null)
                {
                    var current = forecast.CurrentTemperature.HasValue ? $"{forecast.CurrentTemperature:0}°C trenutno, " : "";
                    weatherLine = $"{current}danas {today.MinTemp:0}–{today.MaxTemp:0}°C";
                }
            }
            catch { /* best-effort, same policy as inspection temperature auto-fill */ }
        }

        return AdvisorContextBuilder.Build(
            hive, apiaryName, inspections, activeDiet, dietCompleted, dietTotal, openTodos, queen, seasonYield,
            latestTreatment, pastureLine, weatherLine);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private int RequireUser() =>
        _currentUser.UserId ?? throw new ForbiddenAccessException("Morate biti prijavljeni za korištenje savjetnika.");

    private static string TitleFrom(string message)
    {
        var t = message.Trim();
        return t.Length <= 60 ? t : t[..60].TrimEnd() + "…";
    }

    private static AdvisorConversationSummaryDto ToSummary(AdvisorConversation c) =>
        new(c.Id, c.Title, c.BeehiveId, c.Beehive?.Name, c.UpdatedAt ?? c.CreatedAt, c.CreatedAt);

    private static AdvisorConversationDetailDto ToDetail(AdvisorConversation c) =>
        new(c.Id, c.Title, c.BeehiveId, c.Beehive?.Name,
            c.Messages.Count > 0 ? c.Messages[^1].CreatedAt : c.UpdatedAt ?? c.CreatedAt,
            c.CreatedAt,
            c.Messages.Select(ToMessageDto).ToList());

    private static AdvisorMessageDto ToMessageDto(AdvisorMessage m) =>
        new(m.Id, m.Role.ToString(), m.Content, m.CreatedAt);
}
