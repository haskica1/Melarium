using System.Collections.Concurrent;
using Melarium.Application.Features.Weather.DTOs;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Weather;

/// <summary>
/// In-process forecast cache (SPEC-29), keyed on coordinates rounded to ~1 km. The dashboard is the
/// landing page, so without it every app open asked Open-Meteo for every apiary — whose free tier is
/// not meant for that volume. The alert scan, the weekly summary and the apiary page share the same
/// entries. Lifetime <c>Weather:CacheMinutes</c> (default 60). Failures are never cached.
/// </summary>
/// <remarks>
/// Only the forecast. The current temperature an inspection stamps on itself keeps its own live call.
/// Single-instance by design, like the background workers.
/// </remarks>
public sealed class WeatherForecastCache
{
    private const int MaxEntries = 5000;

    private readonly ConcurrentDictionary<(double Lat, double Lon), Entry> _entries = new();
    private readonly TimeSpan _ttl;
    private readonly TimeProvider _time;

    public WeatherForecastCache(IConfiguration config, TimeProvider time)
    {
        _ttl = TimeSpan.FromMinutes(int.TryParse(config["Weather:CacheMinutes"], out var m) && m > 0 ? m : 60);
        _time = time;
    }

    public async Task<WeatherForecastDto> GetOrFetchAsync(
        double latitude, double longitude, Func<Task<WeatherForecastDto>> fetch)
    {
        var key = (Math.Round(latitude, 2), Math.Round(longitude, 2));
        var now = _time.GetUtcNow();

        if (_entries.TryGetValue(key, out var hit) && hit.Expires > now) return hit.Value;

        var value = await fetch();
        _entries[key] = new Entry(value, now + _ttl);

        if (_entries.Count > MaxEntries)
            foreach (var (k, e) in _entries)
                if (e.Expires <= now) _entries.TryRemove(k, out _);

        return value;
    }

    private sealed record Entry(WeatherForecastDto Value, DateTimeOffset Expires);
}
