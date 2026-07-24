using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Pastures;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>Pasture registry (SPEC-10): the delete guard protects move history.</summary>
public class PastureServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly IPlanGuard _plan = Substitute.For<IPlanGuard>();

    private PastureService Service(int? organizationId = 1) =>
        new(_uow, new TestCurrentUser { UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = organizationId }, _access, _plan);

    [Fact]
    public async Task Delete_ReferencedPasture_ThrowsValidation_NothingDeleted()
    {
        _uow.Pastures.GetByIdAsync(5).Returns(new Pasture { Id = 5, OrganizationId = 1, Name = "Kadulja" });
        _uow.Pastures.HasReferencesAsync(5).Returns(true);

        await Assert.ThrowsAsync<ValidationException>(() => Service().DeleteAsync(5));
        await _uow.Pastures.DidNotReceive().DeleteAsync(Arg.Any<Pasture>());
    }

    [Fact]
    public async Task Delete_UnreferencedPasture_Deletes()
    {
        var pasture = new Pasture { Id = 5, OrganizationId = 1, Name = "Kadulja" };
        _uow.Pastures.GetByIdAsync(5).Returns(pasture);
        _uow.Pastures.HasReferencesAsync(5).Returns(false);

        await Service().DeleteAsync(5);

        await _uow.Pastures.Received(1).DeleteAsync(pasture);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task GetAll_CallerWithoutOrganization_ReturnsEmpty()
    {
        var result = await Service(organizationId: null).GetAllAsync();

        Assert.Empty(result);
        await _uow.Pastures.DidNotReceive().GetByOrganizationWithCountsAsync(Arg.Any<int>());
    }
}
