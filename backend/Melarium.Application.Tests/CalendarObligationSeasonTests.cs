using System.Linq.Expressions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Calendar;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The derived "preporučeni pregled" in the agenda and the ICS feed follows the same season policy as
/// the alerts (SPEC-29): no recommendation in winter, and after winter the clock starts with spring.
/// </summary>
public class CalendarObligationSeasonTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICalendarAccessResolver _resolver = Substitute.For<ICalendarAccessResolver>();
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    private static readonly CalendarUserContext Ctx = new(1, UserRole.OrganizationAdmin, 1, null);
    private static readonly CalendarCategories InspectionsOnly = new(false, false, false, true);

    public CalendarObligationSeasonTests()
    {
        _resolver.ResolveAsync(Arg.Any<CalendarUserContext>()).Returns(new CalendarScope
        {
            ApiaryIds = [1],
            BeehiveIds = [10],
            BeehiveNames = new() { [10] = "K1" },
            ApiaryNames = new() { [1] = "Pčelinjak A" },
        });
        _uow.Organizations.GetByIdAsync(1).Returns(new Organization { Id = 1 });
        _uow.Beehives.FindAsync(Arg.Any<Expression<Func<Beehive, bool>>>())
            .Returns(new[] { new Beehive { Id = 10, Name = "K1", ApiaryId = 1, CreatedAt = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc) } });
        _uow.Inspections.FindAsync(Arg.Any<Expression<Func<Inspection, bool>>>())
            .Returns(new[] { new Inspection { BeehiveId = 10, Date = new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc) } });
    }

    private CalendarObligationService Service() =>
        new(_uow, _resolver, _config, TestSeasons.Policy(_config), TestSeasons.Calendar(_config));

    [Fact]
    public async Task Winter_Agenda_RecommendsNoInspection()
    {
        var items = await Service().GatherAsync(Ctx, new DateOnly(2027, 1, 12), new DateOnly(2027, 1, 12), InspectionsOnly);

        Assert.DoesNotContain(items, o => o.Kind == ObligationKind.InspectionDue);
    }

    [Fact]
    public async Task Feed_RecommendsTheFirstSpringInspection_ThirtyDaysIntoSpring()
    {
        // Last inspection 10 Oct; winter 15 Nov – 15 Feb does not count; spring threshold is 30 days.
        var items = await Service().GatherAsync(Ctx, new DateOnly(2026, 11, 20), new DateOnly(2027, 3, 31), InspectionsOnly);

        var due = Assert.Single(items, o => o.Kind == ObligationKind.InspectionDue);
        Assert.Equal(new DateOnly(2027, 3, 18), due.Date);
    }
}
