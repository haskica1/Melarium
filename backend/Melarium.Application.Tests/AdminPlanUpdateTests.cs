using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Admin;
using Melarium.Application.Features.Admin.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>Manual plan activation by SystemAdmin (SPEC-09 v1): accepts all five plans and
/// treats a null expiry as lifetime (early adopters / Partner orgs).</summary>
public class AdminPlanUpdateTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly AdminService _service;

    public AdminPlanUpdateTests()
    {
        _service = new AdminService(_uow, Substitute.For<INotificationService>());
    }

    [Fact]
    public async Task UpdatePlan_AcceptsHiddenPartnerPlan_WithLifetimeExpiry()
    {
        var org = new Organization { Id = 7, Name = "Prijatelj", Plan = PlanType.Free };
        _uow.Organizations.GetWithDetailsAsync(7).Returns(org);

        var dto = await _service.UpdateOrganizationPlanAsync(7,
            new UpdateOrganizationPlanDto(PlanType.Partner, PlanValidUntil: null, PlanNotes: "Kum"));

        Assert.Equal(PlanType.Partner, org.Plan);
        Assert.Null(org.PlanValidUntil);      // lifetime
        Assert.Equal("Kum", org.PlanNotes);
        Assert.Equal(PlanType.Partner, dto.Plan);
        Assert.Equal("Partner", dto.PlanName);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UpdatePlan_SetsExpiryForPaidPlan()
    {
        var org = new Organization { Id = 8, Name = "Platiša", Plan = PlanType.Free };
        _uow.Organizations.GetWithDetailsAsync(8).Returns(org);
        var until = new DateTime(2027, 07, 06, 0, 0, 0, DateTimeKind.Utc);

        await _service.UpdateOrganizationPlanAsync(8,
            new UpdateOrganizationPlanDto(PlanType.Standard, until, "Uplatnica #123"));

        Assert.Equal(PlanType.Standard, org.Plan);
        Assert.Equal(until, org.PlanValidUntil);
        Assert.Equal("Uplatnica #123", org.PlanNotes);
    }
}
