using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Alerts;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Weather;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The weekly AI summary worker skips organizations whose effective plan lacks the feature
/// (SPEC-09): a Free org is dropped before any data gathering or Groq call.
/// </summary>
public class WeeklySummaryPlanTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private WeeklySummaryService Service()
    {
        var config = Substitute.For<IConfiguration>();
        config["Alerts:WeeklySummary:Enabled"].Returns("true");
        config["Groq:ApiKey"].Returns("dummy-key"); // present so the worker doesn't early-return

        return new WeeklySummaryService(
            new HttpClient(),
            _uow,
            Substitute.For<INotificationService>(),
            Substitute.For<IWeatherService>(),
            config);
    }

    [Fact]
    public async Task Run_SkipsFreeOrg_ButProcessesPaidOrg()
    {
        var freeOrg = new Organization { Id = 2, Name = "Free", Plan = PlanType.Free };
        var paidOrg = new Organization { Id = 1, Name = "Paid", Plan = PlanType.Standard };
        _uow.Organizations.GetAllAsync().Returns(new[] { freeOrg, paidOrg });

        // Paid org proceeds to gather apiaries (empty → harmless continue). Free org must not.
        _uow.Apiaries.GetAllByOrganizationAsync(Arg.Any<int>()).Returns(new List<Apiary>());

        await Service().RunAsync();

        await _uow.Apiaries.Received(1).GetAllByOrganizationAsync(1);   // paid org gathered
        await _uow.Apiaries.DidNotReceive().GetAllByOrganizationAsync(2); // free org skipped
    }

    [Fact]
    public async Task Run_SkipsExpiredTrialOrg()
    {
        var expired = new Organization
        {
            Id = 3,
            Name = "Expired trial",
            Plan = PlanType.Pro,
            PlanValidUntil = DateTime.UtcNow.AddDays(-1), // effectively Free now
        };
        _uow.Organizations.GetAllAsync().Returns(new[] { expired });

        await Service().RunAsync();

        await _uow.Apiaries.DidNotReceive().GetAllByOrganizationAsync(3);
    }
}
