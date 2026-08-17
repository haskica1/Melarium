using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Admin;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The two columns the SystemAdmin organization table answers "is this organization being used?"
/// with: hive count and last activity. Both are derived — never stored — so the only contract worth
/// locking here is that a missing entry means "none / never" rather than being dropped or crashing.
/// </summary>
public class AdminOrganizationMetricsTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly AdminService _service;

    public AdminOrganizationMetricsTests()
    {
        _service = new AdminService(_uow, Substitute.For<INotificationService>(), new SessionRevoker(_uow));
    }

    [Fact]
    public async Task GetAll_CarriesHiveCountAndLastActivityPerOrganization()
    {
        var busy = new Organization { Id = 1, Name = "Zlatna košnica" };
        var quiet = new Organization { Id = 2, Name = "Mirna" };
        var lastSeen = new DateTime(2026, 08, 14, 9, 30, 0, DateTimeKind.Utc);

        _uow.Organizations.GetAllWithDetailsAsync().Returns([busy, quiet]);
        _uow.Organizations.GetBeehiveCountsAsync().Returns(new Dictionary<int, int> { [1] = 42 });
        _uow.Organizations.GetLastActivityAsync().Returns(new Dictionary<int, DateTime> { [1] = lastSeen });

        var dtos = (await _service.GetAllOrganizationsAsync()).ToList();

        var busyDto = dtos.Single(d => d.Id == 1);
        Assert.Equal(42, busyDto.BeehiveCount);
        Assert.Equal(lastSeen, busyDto.LastActivityAt);

        // Absent from both dictionaries — an organization that exists but has never been used.
        var quietDto = dtos.Single(d => d.Id == 2);
        Assert.Equal(0, quietDto.BeehiveCount);
        Assert.Null(quietDto.LastActivityAt);
    }

    [Fact]
    public async Task GetById_AsksForOneOrganizationRatherThanThePlatform()
    {
        var org = new Organization { Id = 5, Name = "Pčelarstvo Haskić" };
        var lastSeen = new DateTime(2026, 08, 16, 18, 0, 0, DateTimeKind.Utc);

        _uow.Organizations.GetWithDetailsAsync(5).Returns(org);
        _uow.Organizations.GetBeehiveCountsAsync(5).Returns(new Dictionary<int, int> { [5] = 7 });
        _uow.Organizations.GetLastActivityAsync(5).Returns(new Dictionary<int, DateTime> { [5] = lastSeen });

        var dto = await _service.GetOrganizationByIdAsync(5);

        Assert.Equal(7, dto.BeehiveCount);
        Assert.Equal(lastSeen, dto.LastActivityAt);
        await _uow.Organizations.Received(1).GetLastActivityAsync(5);
    }
}
