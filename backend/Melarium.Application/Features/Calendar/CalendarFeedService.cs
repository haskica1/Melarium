using System.Security.Cryptography;
using Melarium.Application.Common;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Calendar.DTOs;
using Melarium.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Calendar;

/// <summary>
/// Manages the per-user ICS feed token + calendar settings, and renders the feed document. The token
/// is an opaque 256-bit value stored as-is (the "secret address" model) so the URL can be re-shown;
/// rotating replaces it. The feed itself is one-way and read-only.
/// </summary>
public class CalendarFeedService : ICalendarFeedService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly ICalendarObligationService _obligations;
    private readonly IConfiguration _config;

    public CalendarFeedService(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        ICalendarObligationService obligations,
        IConfiguration config)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _obligations = obligations;
        _config      = config;
    }

    public async Task<CalendarFeedTokenDto> EnsureFeedTokenAsync()
    {
        var s = await GetOrCreateForCurrentUserAsync();
        if (string.IsNullOrEmpty(s.FeedToken))
        {
            s.FeedToken = GenerateToken();
            await _uow.CalendarSettings.UpdateAsync(s);
            await _uow.SaveChangesAsync();
        }
        return new CalendarFeedTokenDto(s.FeedToken!, s.FeedEnabled);
    }

    public async Task<CalendarFeedTokenDto> RotateFeedTokenAsync()
    {
        var s = await GetOrCreateForCurrentUserAsync();
        s.FeedToken = GenerateToken();
        await _uow.CalendarSettings.UpdateAsync(s);
        await _uow.SaveChangesAsync();
        return new CalendarFeedTokenDto(s.FeedToken!, s.FeedEnabled);
    }

    public async Task<CalendarSettingsDto> GetSettingsAsync() => ToDto(await GetOrCreateForCurrentUserAsync());

    public async Task<CalendarSettingsDto> UpdateSettingsAsync(UpdateCalendarSettingsDto dto)
    {
        var s = await GetOrCreateForCurrentUserAsync();
        s.FeedEnabled        = dto.FeedEnabled;
        s.SyncFeedings       = dto.SyncFeedings;
        s.SyncTodos          = dto.SyncTodos;
        s.SyncTreatments     = dto.SyncTreatments;
        s.SyncInspections    = dto.SyncInspections;
        s.DailyAgendaEnabled = dto.DailyAgendaEnabled;
        await _uow.CalendarSettings.UpdateAsync(s);
        await _uow.SaveChangesAsync();
        return ToDto(s);
    }

    public async Task<string?> BuildFeedAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var s = await _uow.CalendarSettings.GetByFeedTokenAsync(token);
        if (s is null || !s.FeedEnabled) return null;

        var user = await _uow.Users.GetByIdAsync(s.UserId);
        if (user is null) return null;

        var ctx  = new CalendarUserContext(user.Id, user.Role, user.OrganizationId, user.ApiaryId);
        var cats = new CalendarCategories(s.SyncFeedings, s.SyncTodos, s.SyncTreatments, s.SyncInspections);

        var tz     = AppTimeZone.Resolve(_config);
        var today  = AppTimeZone.Today(tz);
        var past   = GetInt("CalendarFeed:PastDays", 7);
        var future = GetInt("CalendarFeed:FutureDays", 120);

        var items = await _obligations.GatherAsync(ctx, today.AddDays(-past), today.AddDays(future), cats);

        var hour = GetInt("Reminders:DailyAgenda:LocalHour", 8);
        return IcsWriter.Build(items, tz, AppTimeZone.IanaId(_config), hour, UidHost(), "Melarium — obaveze");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<CalendarSettings> GetOrCreateForCurrentUserAsync()
    {
        var userId = _currentUser.UserId!.Value;
        var s = await _uow.CalendarSettings.GetByUserIdAsync(userId);
        if (s is null)
        {
            s = new CalendarSettings { UserId = userId };
            await _uow.CalendarSettings.AddAsync(s);
            await _uow.SaveChangesAsync();
        }
        return s;
    }

    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private string UidHost()
    {
        var baseUrl = _config["App:PublicBaseUrl"];
        return !string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            ? uri.Host
            : "beehive.app";
    }

    private static CalendarSettingsDto ToDto(CalendarSettings s) => new(
        s.FeedEnabled, s.SyncFeedings, s.SyncTodos, s.SyncTreatments, s.SyncInspections, s.DailyAgendaEnabled);

    private int GetInt(string key, int fallback) => int.TryParse(_config[key], out var v) ? v : fallback;
}
