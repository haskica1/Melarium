using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Treatments;
using Melarium.Application.Features.Treatments.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>Treatment role scoping + entry validation (identical matrix to harvests), plus computed
/// status on the mapped DTO (SPEC-08).</summary>
public class TreatmentServiceTests
{
    private const int ApiaryId = 1;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();

    private static Beehive Hive(int id) => new() { Id = id, Name = $"Košnica {id}", ApiaryId = ApiaryId };

    private static Treatment TreatmentWith(int id, DateTime start, DateTime? end, int withdrawal, params int[] hiveIds) => new()
    {
        Id = id, ApiaryId = ApiaryId, Purpose = TreatmentPurpose.Varroa, ProductName = "Apivar",
        ActiveSubstance = ActiveSubstance.Amitraz, Method = ApplicationMethod.Strips, DosePerHive = "2 trake",
        StartDate = start, EndDate = end, WithdrawalDays = withdrawal,
        Entries = hiveIds.Select(h => new TreatmentEntry { BeehiveId = h }).ToList(),
    };

    private TreatmentService Service(ICurrentUser user) => new(_uow, _access, user);

    [Fact]
    public async Task Create_WithHiveNotInApiary_ThrowsValidation()
    {
        var user = new TestCurrentUser { UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = 1 };
        _uow.Beehives.GetByApiaryIdAsync(ApiaryId).Returns(new[] { Hive(5) });

        var dto = new CreateTreatmentDto
        {
            ApiaryId = ApiaryId, Purpose = TreatmentPurpose.Varroa, ProductName = "Apivar",
            ActiveSubstance = ActiveSubstance.Amitraz, Method = ApplicationMethod.Strips, DosePerHive = "2 trake",
            StartDate = DateTime.UtcNow, WithdrawalDays = 0,
            Entries = [new CreateTreatmentEntryDto { BeehiveId = 99 }],
        };

        await Assert.ThrowsAsync<ValidationException>(() => Service(user).CreateAsync(dto));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task GetAll_AsBeekeeper_ReturnsOnlyTreatmentsContainingAssignedHives()
    {
        var user = new TestCurrentUser { UserId = 7, Role = UserRole.Beekeeper, OrganizationId = 1 };
        _access.GetAssignedBeehiveIdsAsync().Returns(new HashSet<int> { 5 });
        _access.GetAssignedApiaryIdsAsync().Returns(new HashSet<int> { ApiaryId });
        _uow.Treatments.GetByApiaryAsync(ApiaryId, Arg.Any<int?>())
            .Returns(new[]
            {
                TreatmentWith(1, DateTime.UtcNow, null, 0, 5, 6),
                TreatmentWith(2, DateTime.UtcNow, null, 0, 8, 9),
            });

        var result = (await Service(user).GetAllAsync(null, null, null)).ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public async Task GetAll_AsBeekeeper_NoAssignments_ReturnsEmpty()
    {
        var user = new TestCurrentUser { UserId = 7, Role = UserRole.Beekeeper, OrganizationId = 1 };
        _access.GetAssignedBeehiveIdsAsync().Returns(new HashSet<int>());

        Assert.Empty(await Service(user).GetAllAsync(null, null, null));
    }

    [Fact]
    public async Task Create_MapsComputedStatus_InProgress_WhenNoEndDate()
    {
        var user = new TestCurrentUser { UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = 1 };
        _uow.Beehives.GetByApiaryIdAsync(ApiaryId).Returns(new[] { Hive(5) });

        Treatment? added = null;
        _uow.Treatments.AddAsync(Arg.Do<Treatment>(t => { added = t; added.Id = 42; }))
            .Returns(ci => ci.Arg<Treatment>());
        _uow.Treatments.GetWithEntriesAsync(42).Returns(_ => added);

        var dto = new CreateTreatmentDto
        {
            ApiaryId = ApiaryId, Purpose = TreatmentPurpose.Varroa, ProductName = "Apivar",
            ActiveSubstance = ActiveSubstance.Amitraz, Method = ApplicationMethod.Strips, DosePerHive = "2 trake",
            StartDate = DateTime.UtcNow.AddDays(-3), EndDate = null, WithdrawalDays = 42,
            Entries = [new CreateTreatmentEntryDto { BeehiveId = 5 }],
        };

        var result = await Service(user).CreateAsync(dto);

        Assert.Equal(TreatmentStatus.InProgress, result.Status);
        Assert.Equal("U toku", result.StatusName);
        Assert.Equal("Varoa", result.PurposeName);
        Assert.Equal(1, result.HiveCount);
    }
}
