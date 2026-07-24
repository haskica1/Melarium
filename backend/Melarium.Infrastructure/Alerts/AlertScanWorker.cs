using Melarium.Application.Features.Alerts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Melarium.Infrastructure.Alerts;

/// <summary>
/// Fires the daily rule-based alert scan once per day at <c>Alerts:ScanHourUtc</c> (SPEC-04). On
/// Mondays it also runs the weekly AI summary. All rule logic lives in the Application services
/// (<see cref="IAlertRuleService"/> / <see cref="IWeeklySummaryService"/>) so this worker stays thin;
/// each run gets its own DI scope, mirroring <c>EmailNotificationWorker</c>.
/// </summary>
public sealed class AlertScanWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AlertScanWorker> _logger;

    public AlertScanWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<AlertScanWorker> logger)
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

                var rules = scope.ServiceProvider.GetRequiredService<IAlertRuleService>();
                await rules.RunDailyScanAsync(stoppingToken);

                // Weekly AI digest runs on Mondays, after the daily rule scan.
                if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Monday)
                {
                    var weekly = scope.ServiceProvider.GetRequiredService<IWeeklySummaryService>();
                    await weekly.RunAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed scan must never kill the worker — it retries at the next scheduled run.
                _logger.LogError(ex, "Alert scan failed");
            }
        }
    }

    private TimeSpan ComputeDelayUntilNextRun()
    {
        var hour = int.TryParse(_config["Alerts:ScanHourUtc"], out var h) ? Math.Clamp(h, 0, 23) : 5;

        var now = DateTime.UtcNow;
        var next = new DateTime(now.Year, now.Month, now.Day, hour, 0, 0, DateTimeKind.Utc);
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }
}
