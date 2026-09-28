using Melarium.Application.Common.Seasons;
using Melarium.Application.Features.Notifications;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace Melarium.Application.Tests;

/// <summary>
/// The real season calendar and policy with default configuration (Europe/Sarajevo, SPEC-29
/// boundaries), plus a clock that can be set — every seasonal rule depends on the date, so a test
/// that reads the machine's clock would pass in September and fail in December.
/// </summary>
public static class TestSeasons
{
    public static IConfiguration EmptyConfig() => Substitute.For<IConfiguration>();

    public static SeasonCalendar Calendar(IConfiguration? config = null) => new(config ?? EmptyConfig());

    public static NotificationPolicy Policy(IConfiguration? config = null)
    {
        config ??= EmptyConfig();
        return new NotificationPolicy(Calendar(config), config);
    }

    /// <summary>A clock stopped at the given UTC moment (default 05:00 UTC — the alert scan hour).</summary>
    public static FakeTime At(int year, int month, int day, int hourUtc = 5, int minuteUtc = 0) =>
        new(new DateTimeOffset(year, month, day, hourUtc, minuteUtc, 0, TimeSpan.Zero));
}

public sealed class FakeTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
