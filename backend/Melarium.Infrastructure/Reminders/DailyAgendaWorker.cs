using Melarium.Application.Common;
using Melarium.Application.Features.Reminders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Melarium.Infrastructure.Reminders;

/// <summary>
/// Fires the daily 08:00-local obligation agenda (SPEC-11 Faza A.2). Wakes at
/// <c>Reminders:DailyAgenda:LocalHour</c> in <c>App:TimeZone</c> (DST-aware), then delegates to
/// <see cref="IDailyAgendaService"/> in a fresh DI scope — mirroring <c>AlertScanWorker</c>. A failed
/// run is logged and retried the next day; it never kills the worker.
/// </summary>
public sealed class DailyAgendaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<DailyAgendaWorker> _logger;

    public DailyAgendaWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<DailyAgendaWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(ComputeDelayUntilNextRun(), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var agenda = scope.ServiceProvider.GetRequiredService<IDailyAgendaService>();
                await agenda.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed run must never kill the worker — it retries at the next scheduled run.
                _logger.LogError(ex, "Daily agenda run failed");
            }
        }
    }

    private TimeSpan ComputeDelayUntilNextRun()
    {
        var hour = int.TryParse(_config["Reminders:DailyAgenda:LocalHour"], out var h) ? Math.Clamp(h, 0, 23) : 8;
        var tz = AppTimeZone.Resolve(_config);

        var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
        var nextLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, hour, 0, 0, DateTimeKind.Unspecified);
        if (nextLocal <= nowLocal) nextLocal = nextLocal.AddDays(1);

        // Guard against the spring-forward gap so ConvertTimeToUtc can't throw and kill the worker.
        while (tz.IsInvalidTime(nextLocal)) nextLocal = nextLocal.AddHours(1);

        var nextUtc = TimeZoneInfo.ConvertTimeToUtc(nextLocal, tz);
        var delay = nextUtc - DateTime.UtcNow;
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }
}
